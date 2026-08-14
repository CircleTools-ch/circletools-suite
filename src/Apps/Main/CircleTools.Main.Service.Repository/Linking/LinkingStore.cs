using System.Text.Json;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service.Repository.Linking;

/// <summary>
/// EF Core implementation of <see cref="ILinkingStore"/> over the <c>linking</c> schema (FEAT-005).
/// </summary>
/// <remarks>
/// <para>
/// Authorization is the whole game here and it must fail closed. The load-bearing rule is that a
/// <c>my-assignment</c> read <strong>denies</strong> a caller who is a stranger to the group and only
/// returns "nothing to show" to a caller the group already knows — never the reverse, which would leak
/// the secret the app exists to keep (NFR-002, ADR-0020). The distinction is drawn from whether a
/// member-link row for <c>(group, caller)</c> exists at all: an unlink <em>closes</em> that row rather
/// than deleting it, so a former member's read degrades to a graceful "gone" while an outsider's is
/// refused.
/// </para>
/// </remarks>
/// <param name="context">The linking database context.</param>
/// <param name="timeProvider">The server clock — expiry and ordering authority.</param>
public sealed class LinkingStore(LinkingDbContext context, TimeProvider timeProvider) : ILinkingStore
{
    /// <summary>Member-link lifetime, mirroring M2 invitations (FEAT-003).</summary>
    private static readonly TimeSpan _linkLifetime = TimeSpan.FromDays(14);

    /// <inheritdoc />
    public async Task CreateMemberLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var now = timeProvider.GetUtcNow();

        context.MemberLinks.Add(new MemberLinkRow
        {
            Id = record.Id,
            GroupId = record.GroupId,
            GroupName = record.GroupName,
            MemberId = record.MemberId,
            MemberDisplayName = record.MemberDisplayName,
            OrganizerAccountId = record.OrganizerAccountId,
            TargetAccountId = record.TargetAccountId,
            State = MemberLinkState.Pending,
            CreatedAt = now,
            ExpiresAt = now + _linkLifetime,
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateSelfLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var now = timeProvider.GetUtcNow();

        // A self-link is only ever created for the organizer's own verified account, so the target is set.
        var userId = record.TargetAccountId!.Value;

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Idempotent under the one-user-per-group guard: if this account already backs a member here,
        // do not add a second link.
        var alreadyLinked = await context.MemberLinks.AnyAsync(
            m => m.GroupId == record.GroupId && m.TargetAccountId == userId && m.State == MemberLinkState.Linked,
            cancellationToken).ConfigureAwait(false);

        if (!alreadyLinked)
        {
            context.MemberLinks.Add(new MemberLinkRow
            {
                Id = record.Id,
                GroupId = record.GroupId,
                GroupName = record.GroupName,
                MemberId = record.MemberId,
                MemberDisplayName = record.MemberDisplayName,
                OrganizerAccountId = record.OrganizerAccountId,
                TargetAccountId = userId,
                State = MemberLinkState.Linked,
                CreatedAt = now,
                ExpiresAt = now + _linkLifetime,
            });

            AddResolution(record.OrganizerAccountId, record.GroupId, record.MemberId, LinkResolutionKind.Linked, userId, now);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateCircleLinkAsync(MemberLinkRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        var now = timeProvider.GetUtcNow();
        var userId = record.TargetAccountId!.Value;

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // One user backs at most one member per group; the create-from-circle path is idempotent under
        // it, so a re-run does not add a second link.
        var alreadyLinked = await context.MemberLinks.AnyAsync(
            m => m.GroupId == record.GroupId && m.TargetAccountId == userId && m.State == MemberLinkState.Linked,
            cancellationToken).ConfigureAwait(false);

        if (!alreadyLinked)
        {
            context.MemberLinks.Add(new MemberLinkRow
            {
                Id = record.Id,
                GroupId = record.GroupId,
                GroupName = record.GroupName,
                MemberId = record.MemberId,
                MemberDisplayName = record.MemberDisplayName,
                OrganizerAccountId = record.OrganizerAccountId,
                TargetAccountId = userId,
                State = MemberLinkState.Linked,
                CreatedAt = now,
                ExpiresAt = now + _linkLifetime,
            });

            // No resolution: the organizer's group document already carries this Member.UserId.
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingMemberLink>> GetPendingForTargetAsync(
        Guid targetAccountId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        return await context.MemberLinks
            .Where(m => m.TargetAccountId == targetAccountId
                && m.State == MemberLinkState.Pending
                && m.ExpiresAt > now)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new PendingMemberLink(m.Id, m.GroupId, m.GroupName, m.MemberDisplayName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AcceptResult> AcceptAsync(Guid requestId, Guid callerAccountId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.MemberLinks
            .FirstOrDefaultAsync(m => m.Id == requestId, cancellationToken).ConfigureAwait(false);

        // Not addressed to the caller, gone, or expired — all indistinguishable, on purpose (NFR-005).
        if (link is null
            || link.TargetAccountId != callerAccountId
            || link.State != MemberLinkState.Pending
            || link.ExpiresAt <= now)
        {
            return AcceptResult.NotForCaller;
        }

        // One user backs at most one member per group.
        var alreadyLinked = await context.MemberLinks.AnyAsync(
            m => m.GroupId == link.GroupId
                && m.TargetAccountId == callerAccountId
                && m.State == MemberLinkState.Linked,
            cancellationToken).ConfigureAwait(false);

        if (alreadyLinked)
        {
            return AcceptResult.AlreadyLinked;
        }

        link.State = MemberLinkState.Linked;
        AddResolution(link.OrganizerAccountId, link.GroupId, link.MemberId, LinkResolutionKind.Linked, callerAccountId, now);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return AcceptResult.Linked;
    }

    /// <inheritdoc />
    public async Task DeclineAsync(Guid requestId, Guid callerAccountId, CancellationToken cancellationToken)
    {
        var link = await context.MemberLinks
            .FirstOrDefaultAsync(m => m.Id == requestId, cancellationToken).ConfigureAwait(false);

        // Silent and idempotent: a decline emits no resolution, so the organizer cannot tell it from expiry.
        if (link is not null && link.TargetAccountId == callerAccountId && link.State == MemberLinkState.Pending)
        {
            link.State = MemberLinkState.Closed;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RevokeOrUnlinkAsync(Guid requestId, Guid organizerAccountId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.MemberLinks
            .FirstOrDefaultAsync(m => m.Id == requestId && m.OrganizerAccountId == organizerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            return;
        }

        if (link.State == MemberLinkState.Linked && link.TargetAccountId is not null)
        {
            await UnlinkAsync(link, now, cancellationToken).ConfigureAwait(false);
        }
        else if (link.State == MemberLinkState.Pending)
        {
            // Revoke a pending request: no resolution, nothing to unwind.
            link.State = MemberLinkState.Closed;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UnlinkMemberAsync(Guid groupId, Guid memberId, Guid organizerAccountId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.MemberLinks.FirstOrDefaultAsync(
            m => m.GroupId == groupId
                && m.MemberId == memberId
                && m.OrganizerAccountId == organizerAccountId
                && m.State == MemberLinkState.Linked,
            cancellationToken).ConfigureAwait(false);

        if (link is not null)
        {
            await UnlinkAsync(link, now, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // Unlink: the row is closed (not deleted) so a former member's read degrades to "gone", not a 403.
    // The account's assignment and wishlist rows go; the member and its wishlist content survive on the
    // organizer's group document.
    private async Task UnlinkAsync(MemberLinkRow link, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var userId = link.TargetAccountId!.Value;

        await context.Assignments
            .Where(a => a.GroupId == link.GroupId && a.GiverAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberWishlists
            .Where(w => w.GroupId == link.GroupId && w.MemberAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        link.State = MemberLinkState.Closed;
        AddResolution(link.OrganizerAccountId, link.GroupId, link.MemberId, LinkResolutionKind.Unlinked, userId: null, now);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LinkResolution>> DrainResolutionsAsync(Guid organizerAccountId, CancellationToken cancellationToken) =>
        await context.Resolutions
            .Where(r => r.OrganizerAccountId == organizerAccountId)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new LinkResolution(r.Id, r.GroupId, r.MemberId, r.Kind, r.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task AckResolutionsAsync(IReadOnlyList<Guid> resolutionIds, Guid organizerAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolutionIds);
        if (resolutionIds.Count == 0)
        {
            return;
        }

        await context.Resolutions
            .Where(r => r.OrganizerAccountId == organizerAccountId && resolutionIds.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAssignmentsAsync(
        Guid groupId,
        string groupName,
        Guid eventId,
        string eventName,
        Guid organizerAccountId,
        long drawVersion,
        LinkingBudget? budget,
        LinkingEvent? eventDetails,
        IReadOnlyList<AssignmentPublication> assignments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Never let an out-of-order publish overwrite a newer draw of this event: a linked member's next
        // read must be the current pairing, never a previous one (FR-029). Scoped per event (ADR-0042).
        var highestExisting = await context.Assignments
            .Where(a => a.GroupId == groupId && a.EventId == eventId && a.OrganizerAccountId == organizerAccountId)
            .Select(a => (long?)a.DrawVersion)
            .MaxAsync(cancellationToken).ConfigureAwait(false);

        if (highestExisting is { } existing && existing >= drawVersion)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // A re-run replaces this event's prior rows atomically (other events are untouched).
        await context.Assignments
            .Where(a => a.GroupId == groupId && a.EventId == eventId && a.OrganizerAccountId == organizerAccountId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        foreach (var row in assignments)
        {
            context.Assignments.Add(new AssignmentRow
            {
                GroupId = groupId,
                EventId = eventId,
                EventName = eventName,
                GiverAccountId = row.GiverAccountId,
                OrganizerAccountId = organizerAccountId,
                GroupName = groupName,
                RecipientMemberId = row.RecipientMemberId,
                RecipientName = row.RecipientName,
                RecipientWishlistSnapshot = Serialize(row.RecipientWishlistSnapshot),
                BudgetAmount = budget?.Amount,
                BudgetCurrency = budget?.Currency,
                EventDate = eventDetails?.Date,
                EventLocation = eventDetails?.Location,
                EventDescription = eventDetails?.Description,
                DrawVersion = drawVersion,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishGroupEventsAsync(
        Guid groupId,
        string groupName,
        Guid organizerAccountId,
        IReadOnlyList<GroupEventInput> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Replace the organizer's registry for this group wholesale — the client always sends the full
        // current list, so a removed event disappears and a rename lands (ADR-0042).
        await context.GroupEvents
            .Where(e => e.GroupId == groupId && e.OrganizerAccountId == organizerAccountId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        foreach (var occasion in events)
        {
            context.GroupEvents.Add(new GroupEventRow
            {
                GroupId = groupId,
                EventId = occasion.EventId,
                OrganizerAccountId = organizerAccountId,
                GroupName = groupName,
                EventName = occasion.EventName,
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AssignmentReadResult> GetMyAssignmentAsync(Guid groupId, Guid eventId, Guid callerAccountId, CancellationToken cancellationToken)
    {
        var row = await context.Assignments
            .FirstOrDefaultAsync(a => a.GroupId == groupId && a.EventId == eventId && a.GiverAccountId == callerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (row is not null)
        {
            return new AssignmentReadResult(AssignmentReadStatus.Found, await ComposeAsync(row, cancellationToken).ConfigureAwait(false));
        }

        // No assignment for the caller. Draw a hard line: a caller the group knows (any member-link row
        // addressed to them, in any state) gets a graceful "nothing to show"; a stranger is denied.
        var knownToGroup = await context.MemberLinks.AnyAsync(
            m => m.GroupId == groupId && m.TargetAccountId == callerAccountId, cancellationToken).ConfigureAwait(false);

        return new AssignmentReadResult(
            knownToGroup ? AssignmentReadStatus.NotDrawn : AssignmentReadStatus.Denied,
            Assignment: null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentView>> GetMyAssignmentsAsync(Guid callerAccountId, CancellationToken cancellationToken)
    {
        var rows = await context.Assignments
            .Where(a => a.GiverAccountId == callerAccountId)
            .OrderBy(a => a.GroupName).ThenBy(a => a.EventName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var views = new List<AssignmentView>(rows.Count);
        foreach (var row in rows)
        {
            views.Add(await ComposeAsync(row, cancellationToken).ConfigureAwait(false));
        }

        return views;
    }

    /// <inheritdoc />
    public async Task<bool> UpsertMyWishlistAsync(Guid groupId, Guid eventId, string eventName, Guid callerAccountId, IReadOnlyList<string> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        // Only a linked member of the group owns a wishlist there (membership is group-scoped; the
        // wishlist is per event, ADR-0042).
        var membership = await context.MemberLinks
            .FirstOrDefaultAsync(
                m => m.GroupId == groupId && m.TargetAccountId == callerAccountId && m.State == MemberLinkState.Linked,
                cancellationToken).ConfigureAwait(false);

        if (membership is null)
        {
            return false;
        }

        var cleaned = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();

        var existing = await context.MemberWishlists
            .FirstOrDefaultAsync(w => w.GroupId == groupId && w.EventId == eventId && w.MemberAccountId == callerAccountId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.MemberWishlists.Add(new MemberWishlistRow
            {
                GroupId = groupId,
                EventId = eventId,
                EventName = eventName,
                MemberAccountId = callerAccountId,
                GroupName = membership.GroupName,
                Items = Serialize(cleaned),
                Version = 1,
            });
        }
        else
        {
            existing.Items = Serialize(cleaned);
            existing.GroupName = membership.GroupName;
            existing.EventName = eventName;
            existing.Version++;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SelfWishlist>> GetMyWishlistsAsync(Guid callerAccountId, CancellationToken cancellationToken)
    {
        var rows = await context.MemberWishlists
            .Where(w => w.MemberAccountId == callerAccountId)
            .OrderBy(w => w.GroupName).ThenBy(w => w.EventName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Select(w => new SelfWishlist(w.GroupId, w.GroupName, w.EventId, w.EventName, Deserialize(w.Items))).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LinkedMembership>> GetMyMembershipsAsync(Guid callerAccountId, CancellationToken cancellationToken)
    {
        // A membership is group-scoped, but a wishlist is per event: cross the caller's linked groups with
        // the organizer-published event registry so the caller sees one wishlist slot per (group, event),
        // even before any draw (FR-030, ADR-0042).
        var linkedGroups = context.MemberLinks
            .Where(m => m.TargetAccountId == callerAccountId && m.State == MemberLinkState.Linked)
            .Select(m => m.GroupId)
            .Distinct();

        return await context.GroupEvents
            .Where(e => linkedGroups.Contains(e.GroupId))
            .OrderBy(e => e.GroupName).ThenBy(e => e.EventName)
            .Select(e => new LinkedMembership(e.GroupId, e.GroupName, e.EventId, e.EventName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CleanUpAccountAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // 1) The departing account is a linked member elsewhere: unlink every referencing member and
        //    emit the resolution so each organizer's group degrades gracefully (FR-015).
        var linkedElsewhere = await context.MemberLinks
            .Where(m => m.TargetAccountId == userId && m.State == MemberLinkState.Linked)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var link in linkedElsewhere)
        {
            link.State = MemberLinkState.Closed;
            AddResolution(link.OrganizerAccountId, link.GroupId, link.MemberId, LinkResolutionKind.Unlinked, userId: null, now);
        }

        // 2) The account's own giver/wishlist rows go.
        await context.Assignments.Where(a => a.GiverAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberWishlists.Where(w => w.MemberAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // 3) Groups the departing account organized are gone with its sync store: drop everything the
        //    linking store still holds for them, including other members' self-wishlists there.
        var organizedGroupIds = await context.MemberLinks
            .Where(m => m.OrganizerAccountId == userId)
            .Select(m => m.GroupId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (organizedGroupIds.Count > 0)
        {
            await context.MemberWishlists.Where(w => organizedGroupIds.Contains(w.GroupId))
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        await context.Assignments.Where(a => a.OrganizerAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.GroupEvents.Where(e => e.OrganizerAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.MemberLinks.Where(m => m.OrganizerAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Resolutions.Where(r => r.OrganizerAccountId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<AssignmentView> ComposeAsync(AssignmentRow row, CancellationToken cancellationToken)
    {
        // The recipient's *current* wishlist: a linked recipient's own row if present, else the
        // organizer-published snapshot (FR-030).
        var recipientAccount = await context.MemberLinks
            .Where(m => m.GroupId == row.GroupId && m.MemberId == row.RecipientMemberId && m.State == MemberLinkState.Linked)
            .Select(m => m.TargetAccountId)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> wishlist = Deserialize(row.RecipientWishlistSnapshot);

        if (recipientAccount is { } account)
        {
            // The recipient's own wishlist is per event (ADR-0042): compose it for this event only.
            var own = await context.MemberWishlists
                .Where(w => w.GroupId == row.GroupId && w.EventId == row.EventId && w.MemberAccountId == account)
                .Select(w => w.Items)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (own is not null)
            {
                wishlist = Deserialize(own);
            }
        }

        var budget = row.BudgetAmount is { } amount && row.BudgetCurrency is { } currency
            ? new LinkingBudget(amount, currency)
            : null;
        var @event = row.EventLocation is not null || row.EventDescription is not null || row.EventDate is not null
            ? new LinkingEvent(row.EventDate, row.EventLocation ?? string.Empty, row.EventDescription ?? string.Empty)
            : null;

        return new AssignmentView(row.GroupId, row.GroupName, row.EventId, row.EventName, row.RecipientName, wishlist, budget, @event);
    }

    private void AddResolution(Guid organizerAccountId, Guid groupId, Guid memberId, LinkResolutionKind kind, Guid? userId, DateTimeOffset now) =>
        context.Resolutions.Add(new LinkResolutionRow
        {
            Id = Guid.NewGuid(),
            OrganizerAccountId = organizerAccountId,
            GroupId = groupId,
            MemberId = memberId,
            Kind = kind,
            UserId = userId,
            CreatedAt = now,
        });

    private static string Serialize(IReadOnlyList<string> items) => JsonSerializer.Serialize(items);

    private static List<string> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
