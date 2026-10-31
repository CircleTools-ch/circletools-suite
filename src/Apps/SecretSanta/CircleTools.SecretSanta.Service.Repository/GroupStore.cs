using System.Text.Json;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Entries;
using CircleTools.SecretSanta.Service.Core.Groups;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.SecretSanta.Service.Repository;

/// <summary>
/// EF Core implementation of <see cref="IGroupStore"/> (ADR-0086). Every write that touches a document locks the
/// group row (<c>SELECT … FOR UPDATE</c>) inside a transaction and re-applies the member entries before storing, so
/// a push and a concurrent entry write can't lose each other's changes (review finding C3).
/// </summary>
/// <param name="context">The context.</param>
/// <param name="clock">The clock.</param>
public sealed class GroupStore(SecretSantaDbContext context, TimeProvider clock) : IGroupStore
{
    private static readonly JsonSerializerOptions EntryJson = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<bool> CreateAsync(
        OnlineGroup group,
        RoleAssignment owner,
        string? ownerSecretHash,
        string? ownerSecretCipher,
        string? creatorKeyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(owner);

        if (await context.Groups.AnyAsync(g => g.GroupId == group.GroupId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var now = clock.GetUtcNow();
        context.Groups.Add(new GroupRow
        {
            GroupId = group.GroupId,
            Version = group.Version,
            SchemaVersion = group.SchemaVersion,
            Body = GroupDocument.Serialize(group.Document),
            JoinEnabled = false,
            CreatorKeyHash = creatorKeyHash,
            UpdatedBy = owner.Id,
            CreatedAt = now,
            UpdatedAt = now,
            LastActivityAt = now,
        });
        context.RoleAssignments.Add(ToRow(owner, ownerSecretHash, ownerSecretCipher));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent create of the same id won.
            context.ChangeTracker.Clear();
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<OnlineGroupDetails?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var row = await context.Groups.AsNoTracking()
            .SingleOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var assignments = await context.RoleAssignments.AsNoTracking()
            .Where(a => a.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var entries = await context.MemberEntries.AsNoTracking()
            .Where(e => e.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new OnlineGroupDetails(ToGroup(row), [.. assignments.Select(ToAssignment)], [.. entries.Select(ToEntry)]);
    }

    /// <inheritdoc />
    public Task<bool> IsCreatorAsync(Guid groupId, string creatorKeyHash, CancellationToken cancellationToken = default) =>
        context.Groups.AnyAsync(g => g.GroupId == groupId && g.CreatorKeyHash == creatorKeyHash, cancellationToken);

    /// <inheritdoc />
    public async Task<RoleAssignment?> FindAssignmentAsync(
        Guid groupId,
        Guid? accountId,
        IReadOnlyCollection<string> secretHashes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretHashes);
        if (accountId is null && secretHashes.Count == 0)
        {
            return null;
        }

        var hashes = secretHashes.ToList();
        var rows = await context.RoleAssignments.AsNoTracking()
            .Where(a => a.GroupId == groupId
                && ((accountId != null && a.AccountId == accountId)
                    || (a.SecretHash != null && hashes.Contains(a.SecretHash))))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // An organizer link and a membership may both match one request; the stronger role wins, active first.
        var best = rows
            .OrderBy(a => a.State == RoleAssignmentState.Active ? 0 : 1)
            .ThenBy(a => a.Role)
            .FirstOrDefault();
        return best is null ? null : ToAssignment(best);
    }

    /// <inheritdoc />
    public Task<string?> GetOrganizerSecretCipherAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        context.RoleAssignments.AsNoTracking()
            .Where(a => a.GroupId == groupId && a.Role == GroupRole.Owner && a.PrincipalKind == PrincipalKind.OrganizerLink)
            .Select(a => a.SecretCipher)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PushResult> PushAsync(
        Guid groupId,
        long baseVersion,
        int schemaVersion,
        GroupState document,
        Guid writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var row = await LockAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return new PushResult(WriteStatus.NotFound, 0, null, false);
        }

        if (schemaVersion < row.SchemaVersion)
        {
            return new PushResult(WriteStatus.Conflict, row.Version, null, false);
        }

        // Newest write wins for now (ADR-0016); the base version is on the wire so a later rule can refuse a stale
        // push instead, without a protocol change (ADR-0086, several admins). Today it only guards entry deletion below.

        var entries = await EntriesAsync(groupId, cancellationToken).ConfigureAwait(false);
        var (merged, rewritten) = MemberEntryMerge.Apply(document, entries);

        // A member the organizer removed keeps no entry row either — what they wrote is deleted with them (ADR-0052).
        // An archived member was removed for certain. A member missing from the document was only removed if this
        // push saw the latest version: a stale copy (say, from before someone joined under a new name) also lacks
        // them, and that must not destroy what they wrote. Old and new app versions push the same way.
        var stale = baseVersion < row.Version;
        var archived = merged.Members.Where(m => m.IsArchived).Select(m => m.Id).ToHashSet();
        var present = merged.Members.Select(m => m.Id).ToHashSet();
        var removed = entries.Select(e => e.MemberId)
            .Where(id => archived.Contains(id) || (!present.Contains(id) && !stale))
            .ToList();
        if (removed.Count > 0)
        {
            await context.MemberEntries.Where(e => e.GroupId == groupId && removed.Contains(e.MemberId))
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        var now = clock.GetUtcNow();
        row.Body = GroupDocument.Serialize(merged);
        row.SchemaVersion = schemaVersion;
        row.Version++;
        row.UpdatedBy = writer == Guid.Empty ? null : writer;
        row.UpdatedAt = now;
        row.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PushResult(WriteStatus.Applied, row.Version, merged, rewritten);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.RevealTokens.Where(t => t.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberEntries.Where(e => e.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.JoinLinks.Where(l => l.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.RoleAssignments.Where(a => a.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Groups.Where(g => g.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task TouchAsync(Guid groupId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // Once a day is enough for a window counted in months, and keeps reads from writing on every request.
        var stale = now - TimeSpan.FromDays(1);
        return context.Groups
            .Where(g => g.GroupId == groupId && g.LastActivityAt < stale)
            .ExecuteUpdateAsync(set => set.SetProperty(g => g.LastActivityAt, now), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListOwnerlessInactiveAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        await context.Groups
            .Where(g => g.LastActivityAt < cutoff)
            .Where(g => !context.RoleAssignments.Any(a =>
                a.GroupId == g.GroupId && a.Role == GroupRole.Owner && a.PrincipalKind == PrincipalKind.Account))
            .Select(g => g.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<long?> WriteEntryAsync(Guid groupId, MemberEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var row = await LockAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (row is null || !GroupDocument.TryDeserialize(row.Body, out var document))
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var stored = await context.MemberEntries
            .SingleOrDefaultAsync(e => e.GroupId == groupId && e.MemberId == entry.MemberId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            stored = new MemberEntryRow { GroupId = groupId, MemberId = entry.MemberId };
            context.MemberEntries.Add(stored);
        }

        stored.GiftNote = string.IsNullOrWhiteSpace(entry.GiftNote) ? null : entry.GiftNote.Trim();
        stored.Wishlists = JsonSerializer.Serialize(entry.Wishlists ?? [], EntryJson);
        stored.KeptBy = entry.KeptBy;
        stored.Version++;
        stored.UpdatedAt = now;

        var (merged, _) = MemberEntryMerge.Apply(document!, [entry]);
        row.Body = GroupDocument.Serialize(merged);
        row.Version++;
        row.UpdatedAt = now;
        row.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return row.Version;
    }

    /// <inheritdoc />
    public Task DeleteEntryAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default) =>
        context.MemberEntries.Where(e => e.GroupId == groupId && e.MemberId == memberId).ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> AddAssignmentAsync(RoleAssignment assignment, string? secretHash, string? secretCipher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var taken = await context.RoleAssignments.AnyAsync(
            a => (a.GroupId == assignment.GroupId
                    && ((assignment.AccountId != null && a.AccountId == assignment.AccountId)
                        || (assignment.MemberId != null && a.MemberId == assignment.MemberId)))
                || (secretHash != null && a.SecretHash == secretHash),
            cancellationToken).ConfigureAwait(false);
        if (taken)
        {
            return false;
        }

        context.RoleAssignments.Add(ToRow(assignment, secretHash, secretCipher));
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent claim of the same member or account won (first claim locks, ADR-0058).
            context.ChangeTracker.Clear();
            return false;
        }
    }

    /// <inheritdoc />
    public Task ActivateAsync(Guid assignmentId, CancellationToken cancellationToken = default) =>
        context.RoleAssignments.Where(a => a.Id == assignmentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.State, RoleAssignmentState.Active).SetProperty(a => a.ExpiresAt, (DateTimeOffset?)null),
                cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteExpiredInvitationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        context.RoleAssignments
            .Where(a => a.State == RoleAssignmentState.Invited && a.ExpiresAt != null && a.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.RoleAssignments.SingleOrDefaultAsync(a => a.Id == assignmentId, cancellationToken).ConfigureAwait(false);
        if (row is not null)
        {
            if (row.MemberId is { } memberId)
            {
                // What the member wrote stays merged in the document; only the entry row — "they keep it" — goes.
                await context.MemberEntries.Where(e => e.GroupId == row.GroupId && e.MemberId == memberId)
                    .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }

            context.RoleAssignments.Remove(row);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReplacePrincipalAsync(RoleAssignment assignment, string? secretHash, string? secretCipher, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var row = await context.RoleAssignments.SingleOrDefaultAsync(a => a.Id == assignment.Id, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.PrincipalKind = assignment.PrincipalKind;
        row.AccountId = assignment.AccountId;
        row.MemberId = assignment.MemberId;
        row.Route = assignment.Route;
        row.SecretHash = secretHash;
        row.SecretCipher = secretCipher;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleAssignment>> ListAssignmentsOfAccountAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        [.. (await context.RoleAssignments.AsNoTracking()
            .Where(a => a.AccountId == accountId)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToAssignment)];

    /// <inheritdoc />
    public async Task<IReadOnlyList<OnlineGroup>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        var ids = groupIds.ToList();
        return [.. (await context.Groups.AsNoTracking()
            .Where(g => ids.Contains(g.GroupId))
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToGroup)];
    }

    /// <inheritdoc />
    public async Task<string?> SetJoinLinkAsync(Guid groupId, bool enabled, string newTokenHash, string newTokenCipher, CancellationToken cancellationToken = default)
    {
        var group = await context.Groups.SingleOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        group.JoinEnabled = enabled;
        var active = await context.JoinLinks.SingleOrDefaultAsync(l => l.GroupId == groupId && l.Active, cancellationToken).ConfigureAwait(false);
        if (enabled && active is null)
        {
            active = new JoinLinkRow { TokenHash = newTokenHash, TokenCipher = newTokenCipher, GroupId = groupId, Active = true, CreatedAt = clock.GetUtcNow() };
            context.JoinLinks.Add(active);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return enabled ? active?.TokenCipher : null;
    }

    /// <inheritdoc />
    public async Task RotateJoinLinkAsync(Guid groupId, string newTokenHash, string newTokenCipher, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.JoinLinks.Where(l => l.GroupId == groupId && l.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Active, false), cancellationToken).ConfigureAwait(false);
        context.JoinLinks.Add(new JoinLinkRow { TokenHash = newTokenHash, TokenCipher = newTokenCipher, GroupId = groupId, Active = true, CreatedAt = clock.GetUtcNow() });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetJoinLinkCipherAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var enabled = await context.Groups.AnyAsync(g => g.GroupId == groupId && g.JoinEnabled, cancellationToken).ConfigureAwait(false);
        return enabled
            ? await context.JoinLinks.AsNoTracking()
                .Where(l => l.GroupId == groupId && l.Active)
                .Select(l => l.TokenCipher)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <inheritdoc />
    public async Task<Guid?> ResolveJoinLinkAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var groupId = await context.JoinLinks.AsNoTracking()
            .Where(l => l.TokenHash == tokenHash && l.Active)
            .Select(l => (Guid?)l.GroupId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return groupId is { } id && await context.Groups.AnyAsync(g => g.GroupId == id && g.JoinEnabled, cancellationToken).ConfigureAwait(false)
            ? id
            : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RevealToken>> EnsureRevealTokensAsync(
        Guid groupId,
        IReadOnlyCollection<(Guid EventId, Guid GiverMemberId)> givers,
        Func<(string Token, string Hash, string Cipher)> mint,
        Func<string, string> decrypt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(givers);
        ArgumentNullException.ThrowIfNull(mint);
        ArgumentNullException.ThrowIfNull(decrypt);

        var existing = await context.RevealTokens
            .Where(t => t.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var result = new List<RevealToken>(givers.Count);
        foreach (var (eventId, giver) in givers.Distinct())
        {
            var row = existing.FirstOrDefault(t => t.EventId == eventId && t.GiverMemberId == giver);
            if (row is not null)
            {
                result.Add(new RevealToken(eventId, giver, decrypt(row.TokenCipher)));
                continue;
            }

            var (token, hash, cipher) = mint();
            context.RevealTokens.Add(new RevealTokenRow
            {
                TokenHash = hash,
                TokenCipher = cipher,
                GroupId = groupId,
                EventId = eventId,
                GiverMemberId = giver,
                CreatedAt = clock.GetUtcNow(),
            });
            result.Add(new RevealToken(eventId, giver, token));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public Task<RevealTarget?> ResolveRevealTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.RevealTokens.AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => new RevealTarget(t.GroupId, t.EventId, t.GiverMemberId))
            .FirstOrDefaultAsync(cancellationToken);

    private Task<GroupRow?> LockAsync(Guid groupId, CancellationToken cancellationToken) =>
        context.Groups
            .FromSqlInterpolated($"SELECT * FROM secretsanta.groups WHERE \"GroupId\" = {groupId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<MemberEntry>> EntriesAsync(Guid groupId, CancellationToken cancellationToken) =>
        [.. (await context.MemberEntries.AsNoTracking()
            .Where(e => e.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToEntry)];

    private static OnlineGroup ToGroup(GroupRow row)
    {
        // The body was written through GroupDocument; an unreadable one is a server fault, not caller input.
        if (!GroupDocument.TryDeserialize(row.Body, out var document))
        {
            throw new InvalidOperationException($"The stored document of group {row.GroupId} can't be read.");
        }

        return new OnlineGroup(row.GroupId, row.Version, row.SchemaVersion, document!, row.JoinEnabled, row.UpdatedAt, row.UpdatedBy);
    }

    private static RoleAssignment ToAssignment(RoleAssignmentRow row) =>
        new(row.Id, row.GroupId, row.Role, row.PrincipalKind, row.AccountId, row.MemberId, row.State, row.Route, row.CreatedAt, row.ExpiresAt);

    private static RoleAssignmentRow ToRow(RoleAssignment assignment, string? secretHash, string? secretCipher) =>
        new()
        {
            Id = assignment.Id,
            GroupId = assignment.GroupId,
            Role = assignment.Role,
            PrincipalKind = assignment.PrincipalKind,
            AccountId = assignment.AccountId,
            SecretHash = secretHash,
            SecretCipher = secretCipher,
            MemberId = assignment.MemberId,
            State = assignment.State,
            Route = assignment.Route,
            CreatedAt = assignment.CreatedAt,
            ExpiresAt = assignment.ExpiresAt,
        };

    private static MemberEntry ToEntry(MemberEntryRow row) =>
        new(
            row.MemberId,
            row.GiftNote,
            JsonSerializer.Deserialize<List<EventWishlist>>(row.Wishlists, EntryJson) ?? [],
            row.KeptBy);
}
