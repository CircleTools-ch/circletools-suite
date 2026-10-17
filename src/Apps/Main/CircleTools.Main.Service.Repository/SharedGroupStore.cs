using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// EF Core implementation of <see cref="ISharedGroupStore"/> over the <c>shared</c> schema
/// (ADR-0058, FEAT-013). The group body is opaque; last-write-wins is by the server clock, mirroring
/// <see cref="SyncStore"/>. The token is the credential — the store never reads a bearer identity.
/// </summary>
/// <param name="context">The shared-group database context.</param>
/// <param name="tokens">The share-token factory (NFR-009 entropy).</param>
/// <param name="cipher">The at-rest protection for tokens (ADR-0061).</param>
/// <param name="timeProvider">The server clock — the only ordering authority.</param>
/// <param name="limits">The abuse-resistance limits (NFR-009).</param>
public sealed class SharedGroupStore(
    SharedGroupDbContext context,
    IShareTokenFactory tokens,
    IShareTokenCipher cipher,
    TimeProvider timeProvider,
    IOptions<SharedGroupLimits> limits) : ISharedGroupStore
{
    private readonly SharedGroupLimits _limits = limits.Value;

    /// <inheritdoc />
    public async Task<CreateSharedGroupResult> CreateAsync(
        CreateSharedGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // This route is anonymous and the group id is disclosed to every join-link holder, so a create
        // must prove it is the group's creator (ADR-0058 security note). No key at all is a refusal.
        if (string.IsNullOrEmpty(request.CreatorKey))
        {
            return Refused;
        }

        if (!WithinLimits(request.Body))
        {
            return new CreateSharedGroupResult(CreateSharedGroupStatus.TooLarge, null);
        }

        var now = timeProvider.GetUtcNow();
        var bodyText = request.Body.GetRawText();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await context.Groups
            .FindAsync([request.GroupId], cancellationToken).ConfigureAwait(false);

        // Sharing a group that already exists is only ever a resumed share by the original creator (a
        // retried ShareByLink whose response was lost, or a device re-uploading). It returns the current
        // links exclusively to a caller that re-presents the matching creator key — never on the strength
        // of the group id alone, which a join-link participant also holds.
        if (existing is not null)
        {
            var resumed = await TryResumeShareAsync(existing, request.CreatorKey, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return resumed;
        }

        context.Groups.Add(new SharedGroupRow
        {
            GroupId = request.GroupId,
            Body = bodyText,
            Version = request.Version,
            CreatorKey = ShareSecretHash.Of(request.CreatorKey),
            ServerModifiedAt = now,
            OwnerAccountId = null,
            CreatedAt = now,
            LastActivityAt = now,
        });

        var adminToken = tokens.Create();
        var joinToken = tokens.Create();
        context.Tokens.Add(NewToken(adminToken, request.GroupId, SharedCapability.Admin, now));
        context.Tokens.Add(NewToken(joinToken, request.GroupId, SharedCapability.Join, now));

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent create won the race on the group-id primary key. Fall back to the resumed-share
            // path so the idempotency contract still holds (the winner and this retry share a key).
            context.ChangeTracker.Clear();
            var winner = await context.Groups
                .FindAsync([request.GroupId], cancellationToken).ConfigureAwait(false);
            var resumed = winner is null
                ? Refused
                : await TryResumeShareAsync(winner, request.CreatorKey, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return resumed;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new CreateSharedGroupResult(
            CreateSharedGroupStatus.Created,
            new MintedLinks(request.GroupId, adminToken, joinToken));
    }

    private static readonly CreateSharedGroupResult Refused = new(CreateSharedGroupStatus.Refused, null);

    /// <summary>
    /// Returns an existing group's active links only to a caller that proves it is the creator (ADR-0058):
    /// the presented key must match the stored one (fixed-time compare), the group must be owner-less, and
    /// an active admin+join pair must exist. Anything else is an indistinguishable refusal.
    /// </summary>
    private async Task<CreateSharedGroupResult> TryResumeShareAsync(
        SharedGroupRow existing,
        string presentedKey,
        CancellationToken cancellationToken)
    {
        // An account-owned group has no live admin link; it must never be re-minted or disclosed here.
        if (existing.OwnerAccountId is not null || string.IsNullOrEmpty(existing.CreatorKey))
        {
            return Refused;
        }

        // Compare the stored hash against the hash of the presented key (ADR-0061); both are equal-length
        // Base64Url strings, so the fixed-time compare still holds.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(existing.CreatorKey), Encoding.UTF8.GetBytes(ShareSecretHash.Of(presentedKey))))
        {
            return Refused;
        }

        var activeTokens = await context.Tokens
            .Where(t => t.GroupId == existing.GroupId && t.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var admin = activeTokens.FirstOrDefault(t => t.Capability == SharedCapability.Admin);
        var join = activeTokens.FirstOrDefault(t => t.Capability == SharedCapability.Join);

        // Decrypt the stored tokens so the resumed share returns the same working links (ADR-0058/0061).
        return admin is not null && join is not null
            ? new CreateSharedGroupResult(
                CreateSharedGroupStatus.Created,
                new MintedLinks(existing.GroupId, cipher.Decrypt(admin.TokenCipher), cipher.Decrypt(join.TokenCipher)))
            : Refused;
    }

    /// <inheritdoc />
    public async Task<SharedGroupView?> GetAsync(string token, ParticipantProof proof, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proof);

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is null)
        {
            return null;
        }

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        if (!tokenRow.Active)
        {
            // A retired admin link (an account claimed admin) resolves to a retired view so the holder
            // learns who took over; any other inactive token (rotated) is indistinguishable from
            // never-existed — no oracle.
            if (tokenRow is { RetiredByClaim: true, Capability: SharedCapability.Admin })
            {
                return new SharedGroupView(
                    group.GroupId, group.Version, ParseBody(group.Body),
                    SharedCapability.Admin, AdminRetired: true, group.AdminClaimedByName);
            }

            return null;
        }

        // Any access keeps the group alive for retention (NFR-010).
        group.LastActivityAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var body = ParseBody(group.Body);
        if (tokenRow.Capability == SharedCapability.Admin)
        {
            return new SharedGroupView(
                group.GroupId, group.Version, body,
                tokenRow.Capability, AdminRetired: false, AdminClaimedByName: null);
        }

        // A join-link reader gets only the pairing of the member it proves to be, and only that member's and
        // their recipient's wishlist and gift note (ADR-0080). An unproven reader gets no pairing at all.
        var claim = await FindProvenClaimAsync(group.GroupId, proof, cancellationToken).ConfigureAwait(false);
        var taken = await TakenMembersAsync(group, body, cancellationToken).ConfigureAwait(false);
        return new SharedGroupView(
            group.GroupId, group.Version, SharedGroupBody.Redact(body, claim?.MemberId),
            tokenRow.Capability, AdminRetired: false, AdminClaimedByName: null,
            ClaimedMemberId: claim?.MemberId, Redacted: true, TakenMembers: taken);
    }

    // The claim a reader proves with its participant key or (for an account claim) its signed-in account.
    private async Task<SharedMemberClaim?> FindProvenClaimAsync(Guid groupId, ParticipantProof proof, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(proof.ParticipantKey))
        {
            var hash = ShareSecretHash.Of(proof.ParticipantKey);
            var byKey = await context.MemberClaims
                .FirstOrDefaultAsync(c => c.GroupId == groupId && c.ParticipantKeyHash == hash, cancellationToken)
                .ConfigureAwait(false);
            if (byKey is not null)
            {
                return byKey;
            }
        }

        if (proof.AccountId is { } account)
        {
            return await context.MemberClaims
                .FirstOrDefaultAsync(
                    c => c.GroupId == groupId && c.ClaimKind == PersonClaimKind.Account && c.AccountId == account,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    // The members a join link can't claim (Q6): already joined, linked to an account, or the organizer.
    private async Task<IReadOnlyList<TakenMember>> TakenMembersAsync(SharedGroupRow group, JsonElement body, CancellationToken cancellationToken)
    {
        var taken = new Dictionary<Guid, TakenReason>();
        foreach (var (memberId, accountId) in SharedGroupBody.LinkedMembers(body))
        {
            taken[memberId] = accountId == group.OwnerAccountId ? TakenReason.Organizer : TakenReason.Linked;
        }

        var claimed = await context.MemberClaims
            .Where(c => c.GroupId == group.GroupId)
            .Select(c => c.MemberId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var memberId in claimed)
        {
            taken.TryAdd(memberId, TakenReason.Joined);
        }

        return [.. taken.Select(t => new TakenMember(t.Key, t.Value))];
    }

    /// <inheritdoc />
    public async Task<SharedWriteResult> PushAsync(
        string token,
        long version,
        JsonElement body,
        CancellationToken cancellationToken = default)
    {
        if (!WithinLimits(body))
        {
            return new SharedWriteResult(SharedWriteStatus.TooLarge, 0);
        }

        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is null)
        {
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        // Only an active admin token may write. A retired or rotated admin token, and any join token,
        // is forbidden.
        if (!tokenRow.Active || tokenRow.Capability != SharedCapability.Admin)
        {
            return new SharedWriteResult(SharedWriteStatus.Forbidden, 0);
        }

        var now = timeProvider.GetUtcNow();
        var bodyText = body.GetRawText();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        var collided = version <= group.Version;

        // Last-write-wins: a clean fast-forward moves the version to the pushed one; a concurrent push
        // arrives later by the server clock and wins in full, and the stored body is discarded in full
        // (ADR-0016). Either way the incoming body is what remains — except participants' own entries,
        // which a stale organizer copy must not undo (ADR-0080).
        var (merged, rewritten) = await ReapplyEntriesAsync(group.GroupId, body, cancellationToken).ConfigureAwait(false);
        group.Body = rewritten ? merged.GetRawText() : bodyText;
        group.Version = Math.Max(version, group.Version);
        group.ServerModifiedAt = now;
        group.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new SharedWriteResult(
            collided ? SharedWriteStatus.Collision : SharedWriteStatus.Applied,
            group.Version,
            rewritten);
    }

    // Re-applies every recorded participant entry of the group to a pushed body (ADR-0080).
    private async Task<(JsonElement Body, bool Changed)> ReapplyEntriesAsync(Guid groupId, JsonElement body, CancellationToken cancellationToken)
    {
        var rows = await context.MemberEntries
            .Where(e => e.GroupId == groupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return (body, false);
        }

        return SharedGroupBody.ApplyEntries(body, [.. rows.Select(r => (r.MemberId, ToEntry(r)))]);
    }

    private static ParticipantEntry ToEntry(SharedMemberEntry row) =>
        new(row.GiftNote, JsonSerializer.Deserialize<List<ParticipantWishlist>>(row.Wishlists, EntryJson) ?? []);

    private static readonly JsonSerializerOptions EntryJson = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<RotatedAdminLink?> RotateAdminAsync(
        string adminToken,
        CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(adminToken, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            // An active token with no group is an invariant violation; treat it as unresolvable.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // Deactivate every currently-active admin token for the group (a rotation, not a claim), then
        // mint a fresh one. Old admin links stop resolving immediately.
        var activeAdmins = await context.Tokens
            .Where(t => t.GroupId == tokenRow.GroupId && t.Active && t.Capability == SharedCapability.Admin)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var admin in activeAdmins)
        {
            admin.Active = false;
            admin.RetiredByClaim = false;
        }

        var fresh = tokens.Create();
        context.Tokens.Add(NewToken(fresh, tokenRow.GroupId, SharedCapability.Admin, now));
        group.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new RotatedAdminLink(fresh);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(adminToken, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return false;
        }

        var groupId = tokenRow.GroupId;

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await DeleteGroupRowsAsync(groupId, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteOwnedAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var owner = await context.Groups
            .Where(g => g.GroupId == groupId)
            .Select(g => new { g.OwnerAccountId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Already gone: the delete a device queued has settled (idempotent, so a retry drains its queue).
        if (owner is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (owner.OwnerAccountId != accountId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await DeleteGroupRowsAsync(groupId, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Removes a group's online copy across its tables; the caller holds the transaction, so a mid-way
    // failure never leaves a token-less, unreachable group behind.
    private async Task DeleteGroupRowsAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await context.RevealSlices.Where(s => s.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberEntries.Where(e => e.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberClaims.Where(c => c.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Tokens.Where(t => t.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Groups.Where(g => g.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>?> GetClaimedMembersAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true })
        {
            return null;
        }

        var exists = await context.Groups
            .AnyAsync(g => g.GroupId == tokenRow.GroupId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            return null;
        }

        return await context.MemberClaims
            .Where(c => c.GroupId == tokenRow.GroupId)
            .Select(c => c.MemberId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetSelfMaintainedMembersAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true })
        {
            return [];
        }

        return await context.MemberEntries
            .Where(e => e.GroupId == tokenRow.GroupId)
            .Select(e => e.MemberId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PersonClaimResult> ClaimPersonAsync(
        string token,
        ClaimPersonRequest request,
        Guid? callerAccountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true })
        {
            return new PersonClaimResult(PersonClaimStatus.NotFound);
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PersonClaimResult(PersonClaimStatus.NotFound);
        }

        // First claim wins and locks: a member already claimed cannot be re-claimed (ADR-0058).
        var existing = await context.MemberClaims
            .FindAsync([tokenRow.GroupId, request.MemberId], cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PersonClaimResult(PersonClaimStatus.AlreadyClaimed);
        }

        // An account claim binds to the signed-in caller, never to an id the client names (ADR-0080);
        // without a caller it is remembered as a device claim. The participant key proves the claim later.
        var asAccount = request.Kind == PersonClaimKind.Account && callerAccountId is not null;
        var participantKey = tokens.Create();
        context.MemberClaims.Add(new SharedMemberClaim
        {
            GroupId = tokenRow.GroupId,
            MemberId = request.MemberId,
            ClaimKind = asAccount ? PersonClaimKind.Account : PersonClaimKind.Device,
            DeviceHandle = request.DeviceHandle,
            AccountId = asAccount ? callerAccountId : null,
            ClaimedAt = now,
            ParticipantKeyHash = ShareSecretHash.Of(participantKey),
        });
        group.LastActivityAt = now;

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent claim won the race on the (group, member) key — the lock held.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return new PersonClaimResult(PersonClaimStatus.AlreadyClaimed);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PersonClaimResult(PersonClaimStatus.Claimed, participantKey);
    }

    /// <inheritdoc />
    public async Task<bool> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(adminToken, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return false;
        }

        // Idempotent: releasing an already-free member still succeeds. The participant's own entry goes with
        // the claim, so the organizer can edit those fields again (ADR-0080).
        await context.MemberEntries
            .Where(e => e.GroupId == tokenRow.GroupId && e.MemberId == memberId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberClaims
            .Where(c => c.GroupId == tokenRow.GroupId && c.MemberId == memberId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReleasePersonAsOwnerAsync(Guid accountId, Guid groupId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var owner = await context.Groups
            .Where(g => g.GroupId == groupId)
            .Select(g => g.OwnerAccountId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (owner != accountId)
        {
            return false;
        }

        await context.MemberEntries
            .Where(e => e.GroupId == groupId && e.MemberId == memberId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberClaims
            .Where(c => c.GroupId == groupId && c.MemberId == memberId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> IssueParticipantKeyAsync(string token, Guid memberId, string deviceHandle, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Join } || string.IsNullOrEmpty(deviceHandle))
        {
            return null;
        }

        var claim = await context.MemberClaims
            .FindAsync([tokenRow.GroupId, memberId], cancellationToken).ConfigureAwait(false);

        // Once only, and only for the device that made the claim: a claim that already has a key, or whose
        // stored handle differs, is refused — the organizer's release is then the recovery (ADR-0080).
        if (claim is not { ClaimKind: PersonClaimKind.Device, ParticipantKeyHash: null, DeviceHandle: { } stored }
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(deviceHandle)))
        {
            return null;
        }

        var key = tokens.Create();
        claim.ParticipantKeyHash = ShareSecretHash.Of(key);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <inheritdoc />
    public async Task<SharedWriteResult> WriteEntryAsync(
        string token,
        ParticipantProof proof,
        ParticipantEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(entry);

        if (!EntryWithinLimits(entry))
        {
            return new SharedWriteResult(SharedWriteStatus.TooLarge, 0);
        }

        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Join })
        {
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        var claim = await FindProvenClaimAsync(group.GroupId, proof, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.Forbidden, 0);
        }

        await RecordEntryAsync(group, claim.MemberId, entry, now, cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SharedWriteResult(SharedWriteStatus.Applied, group.Version);
    }

    // Records a member's own entry (ADR-0080/ADR-0084): upserts the entry row, merges it into the stored body and
    // bumps the version, so every reader — and every later organizer push, which re-applies it — sees it.
    private async Task RecordEntryAsync(
        SharedGroupRow group,
        Guid memberId,
        ParticipantEntry entry,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var wishlists = JsonSerializer.Serialize(entry.Wishlists ?? [], EntryJson);
        var row = await context.MemberEntries
            .FindAsync([group.GroupId, memberId], cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            context.MemberEntries.Add(new SharedMemberEntry
            {
                GroupId = group.GroupId,
                MemberId = memberId,
                GiftNote = Blank(entry.GiftNote),
                Wishlists = wishlists,
                UpdatedAt = now,
            });
        }
        else
        {
            row.GiftNote = Blank(entry.GiftNote);
            row.Wishlists = wishlists;
            row.UpdatedAt = now;
        }

        var (merged, _) = SharedGroupBody.ApplyEntries(ParseBody(group.Body), [(memberId, entry)]);
        group.Body = merged.GetRawText();
        group.Version++;
        group.ServerModifiedAt = now;
        group.LastActivityAt = now;
    }

    /// <inheritdoc />
    public async Task<bool> LeaveAsync(string token, ParticipantProof proof, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proof);

        var tokenRow = await FindTokenAsync(token, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Join })
        {
            return false;
        }

        var claim = await FindProvenClaimAsync(tokenRow.GroupId, proof, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return false;
        }

        await context.MemberEntries
            .Where(e => e.GroupId == claim.GroupId && e.MemberId == claim.MemberId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        context.MemberClaims.Remove(claim);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JoinedSharedGroup>> ListJoinedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var claims = await context.MemberClaims
            .Where(c => c.ClaimKind == PersonClaimKind.Account && c.AccountId == accountId)
            .Select(c => new { c.GroupId, c.MemberId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (claims.Count == 0)
        {
            return [];
        }

        var groupIds = claims.Select(c => c.GroupId).ToList();
        var joinTokens = await context.Tokens
            .Where(t => groupIds.Contains(t.GroupId) && t.Capability == SharedCapability.Join && t.Active)
            .Select(t => new { t.GroupId, t.TokenCipher })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The join token is handed back decrypted from its at-rest ciphertext (ADR-0061) — only to the
        // account that claimed a member, so its other devices can read the group (ADR-0080).
        var joined = new List<JoinedSharedGroup>();
        foreach (var claim in claims)
        {
            var join = joinTokens.FirstOrDefault(t => t.GroupId == claim.GroupId);
            if (join is not null)
            {
                joined.Add(new JoinedSharedGroup(claim.GroupId, cipher.Decrypt(join.TokenCipher), claim.MemberId));
            }
        }

        return joined;
    }

    /// <inheritdoc />
    public async Task<int> ReleaseClaimsOfAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var claims = await context.MemberClaims
            .Where(c => c.AccountId == accountId)
            .Select(c => new { c.GroupId, c.MemberId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var claim in claims)
        {
            await context.MemberEntries
                .Where(e => e.GroupId == claim.GroupId && e.MemberId == claim.MemberId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        return await context.MemberClaims
            .Where(c => c.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SharedGroupOrganizer?> GetOrganizerAsync(Guid groupId, string? adminToken, CancellationToken cancellationToken = default)
    {
        var group = await context.Groups
            .Where(g => g.GroupId == groupId)
            .Select(g => new { g.OwnerAccountId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        var tokenRow = string.IsNullOrEmpty(adminToken) ? null : await FindTokenAsync(adminToken, cancellationToken);
        var adminValid = tokenRow is { Active: true, Capability: SharedCapability.Admin } && tokenRow.GroupId == groupId;
        return new SharedGroupOrganizer(group.OwnerAccountId, adminValid);
    }

    // A participant's entry is bounded like the body (NFR-009): the domain's gift-note length, and caps on
    // events, items and item length.
    private static bool EntryWithinLimits(ParticipantEntry entry)
    {
        if (entry.GiftNote is { Length: > MaxGiftNoteLength })
        {
            return false;
        }

        var wishlists = entry.Wishlists ?? [];
        return wishlists.Count <= MaxEntryEvents
            && wishlists.All(w => (w.Items ?? []).Count <= MaxWishlistItems
                && (w.Items ?? []).All(i => i is null || i.Length <= MaxWishlistItemLength));
    }

    // Mirrors the Secret-Santa domain's gift-note bound (Member.MaxGiftNoteLength) and caps the wishlists.
    private const int MaxGiftNoteLength = 500;
    private const int MaxEntryEvents = 50;
    private const int MaxWishlistItems = 100;
    private const int MaxWishlistItemLength = 500;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <inheritdoc />
    public async Task<bool> ClaimAdminAsync(string adminToken, Guid accountId, string displayName, CancellationToken cancellationToken = default)
    {
        var tokenRow = await FindTokenAsync(adminToken, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null || group.OwnerAccountId is not null)
        {
            // One owner, no ambiguity: a group already claimed cannot be claimed again.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        group.OwnerAccountId = accountId;
        group.AdminClaimedByName = displayName;
        group.LastActivityAt = now;

        // Retire every active admin token (a claim, not a rotation) so holders learn who took over; the
        // join token is untouched.
        var activeAdmins = await context.Tokens
            .Where(t => t.GroupId == tokenRow.GroupId && t.Active && t.Capability == SharedCapability.Admin)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var admin in activeAdmins)
        {
            admin.Active = false;
            admin.RetiredByClaim = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<RotatedAdminLink?> ReleaseAdminAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([groupId], cancellationToken).ConfigureAwait(false);
        if (group is null || group.OwnerAccountId != accountId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        group.OwnerAccountId = null;
        group.AdminClaimedByName = null;
        group.LastActivityAt = now;

        var fresh = tokens.Create();
        context.Tokens.Add(NewToken(fresh, groupId, SharedCapability.Admin, now));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new RotatedAdminLink(fresh);
    }

    /// <inheritdoc />
    public async Task<int> PruneExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        // Owner-less groups only: an account-owned group's lifecycle is the account's (deletion), not
        // the retention sweep's (NFR-010).
        var expired = await context.Groups
            .Where(g => g.OwnerAccountId == null && g.LastActivityAt < olderThan)
            .Select(g => g.GroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var groupId in expired)
        {
            await context.RevealSlices.Where(s => s.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.MemberEntries.Where(e => e.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.MemberClaims.Where(c => c.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.Tokens.Where(t => t.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await context.Groups.Where(g => g.GroupId == groupId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        return expired.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OwnedSharedGroup>> ListOwnedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var owned = await context.Groups
            .Where(g => g.OwnerAccountId == accountId)
            .Select(g => new { g.GroupId, g.Version, g.Body })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return [.. owned.Select(g => new OwnedSharedGroup(g.GroupId, g.Version, ParseBody(g.Body)))];
    }

    /// <inheritdoc />
    public async Task<SharedGroupView?> GetAsOwnerAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await context.Groups.FindAsync([groupId], cancellationToken).ConfigureAwait(false);
        if (group is null || group.OwnerAccountId != accountId)
        {
            return null;
        }

        group.LastActivityAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The owner may share the join link from any of their devices (ADR-0078 Amendment 3): hand back the
        // current join token, decrypted from its at-rest ciphertext (ADR-0061). Only ever on this owner read.
        var join = await context.Tokens
            .Where(t => t.GroupId == groupId && t.Capability == SharedCapability.Join && t.Active)
            .Select(t => t.TokenCipher)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return new SharedGroupView(
            group.GroupId, group.Version, ParseBody(group.Body),
            SharedCapability.Admin, AdminRetired: false, AdminClaimedByName: null,
            JoinToken: string.IsNullOrEmpty(join) ? null : cipher.Decrypt(join));
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        context.Groups.AnyAsync(g => g.GroupId == groupId, cancellationToken);

    /// <inheritdoc />
    public async Task<SharedWriteResult> PushAsOwnerAsync(
        Guid accountId,
        Guid groupId,
        long version,
        JsonElement body,
        CancellationToken cancellationToken = default)
    {
        if (!WithinLimits(body))
        {
            return new SharedWriteResult(SharedWriteStatus.TooLarge, 0);
        }

        var now = timeProvider.GetUtcNow();
        var bodyText = body.GetRawText();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups.FindAsync([groupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        if (group.OwnerAccountId != accountId)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.Forbidden, 0);
        }

        var collided = version <= group.Version;
        var (merged, rewritten) = await ReapplyEntriesAsync(group.GroupId, body, cancellationToken).ConfigureAwait(false);
        group.Body = rewritten ? merged.GetRawText() : bodyText;
        group.Version = Math.Max(version, group.Version);
        group.ServerModifiedAt = now;
        group.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new SharedWriteResult(
            collided ? SharedWriteStatus.Collision : SharedWriteStatus.Applied,
            group.Version,
            rewritten);
    }

    /// <inheritdoc />
    public async Task<int> ReleaseAllOwnedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // Clear ownership on every group this account owns — they revert to owner-less shared-by-link,
        // so join-link participants keep access. The retired admin tokens stay retired (the departing
        // owner can hand off an admin link beforehand via release-to-link); no local copy is touched.
        return await context.Groups
            .Where(g => g.OwnerAccountId == accountId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(g => g.OwnerAccountId, (Guid?)null).SetProperty(g => g.AdminClaimedByName, (string?)null),
                cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllOwnedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var owned = await context.Groups
            .Where(g => g.OwnerAccountId == accountId)
            .Select(g => g.GroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (owned.Count == 0)
        {
            return 0;
        }

        // One transaction across the three tables: a mid-way failure must not leave a token-less,
        // unreachable group behind (the account row it belonged to may be deleted moments later).
        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await context.RevealSlices.Where(s => owned.Contains(s.GroupId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberEntries.Where(e => owned.Contains(e.GroupId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberClaims.Where(c => owned.Contains(c.GroupId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Tokens.Where(t => owned.Contains(t.GroupId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Groups.Where(g => owned.Contains(g.GroupId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return owned.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RevealTokenForKey>?> SetRevealSlicesAsync(
        string adminToken,
        IReadOnlyList<RevealSlice> slices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slices);

        var tokenRow = await FindTokenAsync(adminToken, cancellationToken);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return null;
        }

        return await ReplaceRevealSlicesAsync(tokenRow.GroupId, slices, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RevealTokenForKey>?> SetRevealSlicesAsOwnerAsync(
        Guid accountId,
        Guid groupId,
        IReadOnlyList<RevealSlice> slices,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slices);

        var owns = await context.Groups
            .AnyAsync(g => g.GroupId == groupId && g.OwnerAccountId == accountId, cancellationToken)
            .ConfigureAwait(false);
        return owns ? await ReplaceRevealSlicesAsync(groupId, slices, cancellationToken).ConfigureAwait(false) : null;
    }

    // Replace a group's whole reveal-slice set (ADR-0068): the published set is authoritative, so any
    // slice not in it — a stale reveal token from a previous draw — stops resolving. Bodies are opaque
    // and abuse-capped like the group body (NFR-009).
    private async Task<IReadOnlyList<RevealTokenForKey>?> ReplaceRevealSlicesAsync(
        Guid groupId,
        IReadOnlyList<RevealSlice> slices,
        CancellationToken cancellationToken)
    {
        if (slices.Count > _limits.MaxMembers || slices.Any(s => !WithinLimits(s.Body)))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // A keyed slice keeps the token it already has (ADR-0068 amendment): two organizer devices each mint
        // their own token for a new slice, and whichever publishes first wins — the other adopts it from the
        // answer, so a link handed out from either device keeps resolving.
        var kept = (await context.RevealSlices
                .Where(s => s.GroupId == groupId && s.Key != null)
                .Select(s => new { s.Key, s.TokenCipher })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(s => s.Key!)
            .ToDictionary(g => g.Key, g => cipher.Decrypt(g.First().TokenCipher), StringComparer.Ordinal);

        await context.RevealSlices.Where(s => s.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        var effective = new List<RevealTokenForKey>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slice in slices)
        {
            var token = slice.Key is { } key && kept.TryGetValue(key, out var existing) ? existing : slice.RevealToken;
            if (!seen.Add(token))
            {
                continue; // one row per token
            }

            if (slice.Key is { } k)
            {
                effective.Add(new RevealTokenForKey(k, token));
            }

            context.RevealSlices.Add(new SharedRevealSlice
            {
                // Keyed by the reveal token's hash; the plaintext is kept only as ciphertext (ADR-0061).
                Token = ShareSecretHash.Of(token),
                TokenCipher = cipher.Encrypt(token),
                Key = slice.Key,
                EventId = slice.EventId,
                MemberId = slice.MemberId,
                GroupId = groupId,
                Body = slice.Body.GetRawText(),
                LastActivityAt = now,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return effective;
    }

    /// <inheritdoc />
    public async Task<RevealSliceView?> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(revealToken))
        {
            return null;
        }

        var slice = await context.RevealSlices
            .FindAsync([ShareSecretHash.Of(revealToken)], cancellationToken).ConfigureAwait(false);
        if (slice is null)
        {
            return null;
        }

        // Any access keeps the slice alive for retention (NFR-010), alongside its group.
        slice.LastActivityAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RevealSliceView(ParseBody(slice.Body));
    }

    /// <inheritdoc />
    public async Task<RevealEntryView?> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        if (await ResolveRevealEntryAsync(revealToken, cancellationToken).ConfigureAwait(false) is not { } target)
        {
            return null;
        }

        var (giftNote, items) = SharedGroupBody.ReadEntry(ParseBody(target.Group.Body), target.MemberId, target.EventId);
        return new RevealEntryView(giftNote, items);
    }

    /// <inheritdoc />
    public async Task<SharedWriteResult> WriteRevealEntryAsync(
        string revealToken,
        RevealEntryWrite entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (await ResolveRevealEntryAsync(revealToken, cancellationToken).ConfigureAwait(false) is not { } target)
        {
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        // Only this member's gift note and this event's wishlist change; their wishlists for other events —
        // written from those events' reveal links or by a joined participant — are kept.
        var row = await context.MemberEntries
            .FindAsync([target.Group.GroupId, target.MemberId], cancellationToken).ConfigureAwait(false);
        var others = row is null ? [] : ToEntry(row).Wishlists.Where(w => w.EventId != target.EventId);
        var items = (entry.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();
        var merged = new ParticipantEntry(entry.GiftNote, [.. others, new ParticipantWishlist(target.EventId, items)]);
        if (!EntryWithinLimits(merged))
        {
            return new SharedWriteResult(SharedWriteStatus.TooLarge, 0);
        }

        await RecordEntryAsync(target.Group, target.MemberId, merged, now, cancellationToken).ConfigureAwait(false);
        target.Slice.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SharedWriteResult(SharedWriteStatus.Applied, target.Group.Version);
    }

    // Resolves a reveal token to the member and event it may edit (ADR-0084). Null when the token is unknown,
    // its slice was published without member/event (an older client), the group is gone, or the member is
    // linked to an account — a linked member keeps their wishes through My wishlists (FR-030), not here.
    private async Task<RevealEntryTarget?> ResolveRevealEntryAsync(string revealToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(revealToken))
        {
            return null;
        }

        var slice = await context.RevealSlices
            .FindAsync([ShareSecretHash.Of(revealToken)], cancellationToken).ConfigureAwait(false);
        if (slice is not { MemberId: { } memberId, EventId: { } eventId })
        {
            return null;
        }

        var group = await context.Groups.FindAsync([slice.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null || SharedGroupBody.LinkedMembers(ParseBody(group.Body)).Any(l => l.MemberId == memberId))
        {
            return null;
        }

        return new RevealEntryTarget(slice, group, memberId, eventId);
    }

    private sealed record RevealEntryTarget(SharedRevealSlice Slice, SharedGroupRow Group, Guid MemberId, Guid EventId);

    // Resolve a token row by the hash of the presented token (ADR-0061); a null/empty token never matches.
    private async Task<SharedGroupToken?> FindTokenAsync(string token, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(token)
            ? null
            : await context.Tokens.FindAsync([ShareSecretHash.Of(token)], cancellationToken).ConfigureAwait(false);

    private SharedGroupToken NewToken(string token, Guid groupId, SharedCapability capability, DateTimeOffset now) =>
        new()
        {
            // The row is keyed by the token's hash and keeps its plaintext only as ciphertext (ADR-0061).
            Token = ShareSecretHash.Of(token),
            TokenCipher = cipher.Encrypt(token),
            GroupId = groupId,
            Capability = capability,
            Active = true,
            RetiredByClaim = false,
            CreatedAt = now,
        };

    private bool WithinLimits(JsonElement body)
    {
        // Schema-light abuse caps (NFR-009): a hard byte ceiling, plus a roster ceiling checked only
        // when a members array is present. The store stays opaque otherwise.
        var bytes = Encoding.UTF8.GetByteCount(body.GetRawText());
        if (bytes > _limits.MaxBodyBytes)
        {
            return false;
        }

        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("members", out var members)
            && members.ValueKind == JsonValueKind.Array
            && members.GetArrayLength() > _limits.MaxMembers)
        {
            return false;
        }

        return true;
    }

    private static JsonElement ParseBody(string bodyJson)
    {
        using var document = JsonDocument.Parse(bodyJson);
        return document.RootElement.Clone();
    }
}
