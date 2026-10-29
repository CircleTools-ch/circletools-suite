using System.Text.Json;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Entries;
using CircleTools.SecretSanta.Service.Core.Views;

namespace CircleTools.SecretSanta.Service.Core.Groups;

/// <summary>
/// The Secret-Santa group store's use cases (ADR-0086). Every operation resolves the caller's role assignment
/// first and asks for a <see cref="GroupPermissions"/> — never a role — so a later admin role only changes
/// <see cref="RolePermissions"/>. A caller without the permission, or without any role in the group, gets the same
/// <see cref="WriteStatus.NotFound"/> a missing group gives, so the API is no oracle for group ids.
/// </summary>
/// <param name="store">The persistence port.</param>
/// <param name="secrets">Token minting, hashing and encryption.</param>
/// <param name="accounts">The platform's accounts (invitations, circles).</param>
/// <param name="notifications">The platform's outbox.</param>
/// <param name="limits">The bounds on a stored document and on retention.</param>
/// <param name="clock">The clock.</param>
public sealed class GroupService(
    IGroupStore store,
    IGroupSecrets secrets,
    IGroupAccounts accounts,
    IGroupNotifications notifications,
    GroupLimits limits,
    TimeProvider clock)
{
    /// <summary>How long an email invitation waits for an answer.</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(30);

    /// <summary>The caller's role assignment in a group, if any.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assignment, or <see langword="null"/>.</returns>
    public Task<RoleAssignment?> AccessAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return store.FindAssignmentAsync(groupId, caller.AccountId, SecretHashes(caller), cancellationToken);
    }

    // ---------------------------------------------------------------- create, read, push, delete

    /// <summary>
    /// Makes a group online. Signed in, the account owns it; signed out, an organizer link does, and the creator key
    /// lets a retried create (lost response) recover the links.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="schemaVersion">The document's schema version.</param>
    /// <param name="document">The document.</param>
    /// <param name="creatorKey">The creator key, required without an account.</param>
    /// <param name="joinEnabled">Whether to turn the join link on right away.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the links.</returns>
    public async Task<(WriteStatus Status, GroupLinks? Links)> CreateAsync(
        Caller caller,
        int schemaVersion,
        GroupState document,
        string? creatorKey,
        bool joinEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(document);

        if (schemaVersion is < 1 or > GroupDocument.CurrentSchemaVersion
            || (caller.AccountId is null && string.IsNullOrWhiteSpace(creatorKey))
            || !limits.AreMet(document))
        {
            return (WriteStatus.Invalid, null);
        }

        var creatorKeyHash = string.IsNullOrWhiteSpace(creatorKey) ? null : secrets.Hash(creatorKey);
        var now = clock.GetUtcNow();
        var group = new OnlineGroup(document.Id, 1, schemaVersion, document, joinEnabled, now, null);

        string? organizerToken = null;
        RoleAssignment owner;
        if (caller.AccountId is { } account)
        {
            owner = new RoleAssignment(Guid.CreateVersion7(), document.Id, GroupRole.Owner, PrincipalKind.Account, account, null, RoleAssignmentState.Active, AssignmentRoute.Created, now);
        }
        else
        {
            organizerToken = secrets.NewToken();
            owner = new RoleAssignment(Guid.CreateVersion7(), document.Id, GroupRole.Owner, PrincipalKind.OrganizerLink, null, null, RoleAssignmentState.Active, AssignmentRoute.Created, now);
        }

        var created = await store.CreateAsync(
            group,
            owner,
            organizerToken is null ? null : secrets.Hash(organizerToken),
            organizerToken is null ? null : secrets.Encrypt(organizerToken),
            creatorKeyHash,
            cancellationToken).ConfigureAwait(false);

        if (!created)
        {
            // A retried create: the same creator (same account, or same creator key) gets its links back.
            var existing = await AccessAsync(document.Id, caller, cancellationToken).ConfigureAwait(false);
            var isCreator = existing is { Role: GroupRole.Owner }
                || (creatorKeyHash is not null && await store.IsCreatorAsync(document.Id, creatorKeyHash, cancellationToken).ConfigureAwait(false));
            return isCreator
                ? (WriteStatus.Applied, await LinksAsync(document.Id, cancellationToken).ConfigureAwait(false))
                : (WriteStatus.Conflict, null);
        }

        if (joinEnabled)
        {
            await SetJoinLinkCoreAsync(document.Id, true, cancellationToken).ConfigureAwait(false);
        }

        return (WriteStatus.Applied, await LinksAsync(document.Id, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The whole group, for a caller who may read the document.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The group, the caller's assignment and the links; or <see langword="null"/>.</returns>
    public async Task<(OnlineGroupDetails Details, RoleAssignment Caller, GroupLinks Links)?> GetForOrganizerAsync(
        Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ReadDocument))
        {
            return null;
        }

        var details = await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        await store.TouchAsync(groupId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return (details, access, await LinksAsync(groupId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Stores a new version of the document (member entries re-applied).</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="baseVersion">The version the caller's copy was based on.</param>
    /// <param name="schemaVersion">The document's schema version.</param>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<PushResult> PushAsync(
        Guid groupId, Caller caller, long baseVersion, int schemaVersion, GroupState document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.WriteDocument))
        {
            return new PushResult(WriteStatus.NotFound, 0, null, false);
        }

        // The server stores the document through GroupState, so it can't keep fields from a newer schema than it
        // knows: such a push waits for the server to be updated (it is deployed first).
        if (document.Id != groupId || schemaVersion is < 1 or > GroupDocument.CurrentSchemaVersion || !limits.AreMet(document))
        {
            return new PushResult(WriteStatus.Invalid, 0, null, false);
        }

        return await store.PushAsync(groupId, baseVersion, schemaVersion, document, access.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes the group's online copy.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> DeleteAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.DeleteGroup))
        {
            return WriteStatus.NotFound;
        }

        await store.DeleteAsync(groupId, cancellationToken).ConfigureAwait(false);
        return WriteStatus.Applied;
    }

    // ---------------------------------------------------------------- links and ownership

    /// <summary>Turns the join link on or off.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="enabled">On or off.</param>
    /// <param name="rotate">Whether to replace the link with a new one (the old one stops working).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the links.</returns>
    public async Task<(WriteStatus Status, GroupLinks? Links)> SetJoinLinkAsync(
        Guid groupId, Caller caller, bool enabled, bool rotate, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ManageLinks))
        {
            return (WriteStatus.NotFound, null);
        }

        if (rotate)
        {
            var token = secrets.NewToken();
            await store.RotateJoinLinkAsync(groupId, secrets.Hash(token), secrets.Encrypt(token), cancellationToken).ConfigureAwait(false);
        }

        await SetJoinLinkCoreAsync(groupId, enabled, cancellationToken).ConfigureAwait(false);
        return (WriteStatus.Applied, await LinksAsync(groupId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Replaces the organizer link of a link-owned group; the old link stops working.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the links.</returns>
    public async Task<(WriteStatus Status, GroupLinks? Links)> RotateOrganizerLinkAsync(
        Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ManageLinks))
        {
            return (WriteStatus.NotFound, null);
        }

        var owner = await OwnerAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (owner is not { PrincipalKind: PrincipalKind.OrganizerLink })
        {
            return (WriteStatus.Conflict, null);
        }

        var token = secrets.NewToken();
        await store.ReplacePrincipalAsync(owner, secrets.Hash(token), secrets.Encrypt(token), cancellationToken).ConfigureAwait(false);
        return (WriteStatus.Applied, await LinksAsync(groupId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// An account takes over a link-owned group (it presents the organizer link while signed in): the account
    /// becomes the owner and the organizer link stops working. A member the account already took part as stays its
    /// member.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller: signed in, presenting the organizer link.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> TakeOverAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (caller.AccountId is not { } account || string.IsNullOrWhiteSpace(caller.OrganizerToken))
        {
            return WriteStatus.Invalid;
        }

        var byLink = await store.FindAssignmentAsync(groupId, null, [secrets.Hash(caller.OrganizerToken)], cancellationToken).ConfigureAwait(false);
        if (byLink is not { Role: GroupRole.Owner, PrincipalKind: PrincipalKind.OrganizerLink })
        {
            return WriteStatus.NotFound;
        }

        var memberId = byLink.MemberId;
        var existing = await store.FindAssignmentAsync(groupId, account, [], cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            memberId ??= existing.MemberId;
            await store.RemoveAssignmentAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }

        var taken = byLink with
        {
            PrincipalKind = PrincipalKind.Account,
            AccountId = account,
            MemberId = memberId,
            Route = AssignmentRoute.TakeOver,
        };
        await store.ReplacePrincipalAsync(taken, null, null, cancellationToken).ConfigureAwait(false);
        return WriteStatus.Applied;
    }

    /// <summary>
    /// The owning account hands the group back to an organizer link (no account owns it any more). If the account
    /// was also a member, it keeps taking part as that member.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the new links.</returns>
    public async Task<(WriteStatus Status, GroupLinks? Links)> HandOverAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ManageRoles))
        {
            return (WriteStatus.NotFound, null);
        }

        if (access is not { Role: GroupRole.Owner, PrincipalKind: PrincipalKind.Account })
        {
            return (WriteStatus.Conflict, null);
        }

        await HandOverCoreAsync(access, cancellationToken).ConfigureAwait(false);
        return (WriteStatus.Applied, await LinksAsync(groupId, cancellationToken).ConfigureAwait(false));
    }

    // ---------------------------------------------------------------- participants

    /// <summary>Brings a person into the group as one of its members: own member, invitation or circle.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="request">Who and how.</param>
    /// <param name="inviterName">The caller's display name, for an invitation mail.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> AddParticipantAsync(
        Guid groupId, Caller caller, AddParticipantRequest request, string inviterName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ManageParticipants))
        {
            return WriteStatus.NotFound;
        }

        var details = await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
        var member = details is null ? null : Group.FromState(details.Group.Document).FindMember(request.MemberId);
        if (details is null || member is null || member.IsArchived)
        {
            return WriteStatus.NotFound;
        }

        if (details.Assignments.Any(a => a.MemberId == request.MemberId))
        {
            return WriteStatus.Conflict;
        }

        var now = clock.GetUtcNow();
        switch (request.Route)
        {
            case ParticipantRoute.OwnMember:
                if (caller.AccountId is not { } own)
                {
                    return WriteStatus.Invalid;
                }

                // The organizer's account already holds a role here: it gains the member, it doesn't get a second row.
                var mine = details.Assignments.FirstOrDefault(a => a.AccountId == own);
                if (mine is not null)
                {
                    if (mine.MemberId is not null)
                    {
                        return WriteStatus.Conflict;
                    }

                    await store.ReplacePrincipalAsync(mine with { MemberId = request.MemberId }, null, null, cancellationToken).ConfigureAwait(false);
                    return WriteStatus.Applied;
                }

                return await AddAsync(Participant(groupId, own, request.MemberId, RoleAssignmentState.Active, AssignmentRoute.OwnMember, now), cancellationToken).ConfigureAwait(false);

            case ParticipantRoute.Circle:
                if (caller.AccountId is not { } organizer
                    || request is not { CircleId: { } circle, AccountId: { } target }
                    || !await accounts.ShareCircleAsync(organizer, circle, target, cancellationToken).ConfigureAwait(false))
                {
                    return WriteStatus.Forbidden;
                }

                return details.Assignments.Any(a => a.AccountId == target)
                    ? WriteStatus.Conflict
                    : await AddAsync(Participant(groupId, target, request.MemberId, RoleAssignmentState.Active, AssignmentRoute.Circle, now), cancellationToken).ConfigureAwait(false);

            case ParticipantRoute.Invitation:
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return WriteStatus.Invalid;
                }

                // The answer is the same whether or not the address has an account (NFR-005); only an account already
                // in this group — which the organizer can see — is refused.
                var invited = await accounts.FindByVerifiedEmailAsync(request.Email, cancellationToken).ConfigureAwait(false);
                if (invited is null)
                {
                    return WriteStatus.Applied;
                }

                if (details.Assignments.Any(a => a.AccountId == invited.AccountId))
                {
                    return WriteStatus.Conflict;
                }

                var status = await AddAsync(
                    Participant(groupId, invited.AccountId, request.MemberId, RoleAssignmentState.Invited, AssignmentRoute.Invitation, now) with { ExpiresAt = now + InvitationLifetime },
                    cancellationToken).ConfigureAwait(false);
                if (status == WriteStatus.Applied)
                {
                    await notifications.QueueInvitationAsync(
                        new GroupInvitationMail(invited, details.Group.Document.Name, member.Name, inviterName),
                        cancellationToken).ConfigureAwait(false);
                }

                return status;

            default:
                return WriteStatus.Invalid;
        }
    }

    /// <summary>Releases a participant (or withdraws an invitation); what they wrote stays in the group.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> ReleaseAsync(Guid groupId, Caller caller, Guid memberId, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ManageParticipants))
        {
            return WriteStatus.NotFound;
        }

        var details = await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
        var target = details?.Assignments.FirstOrDefault(a => a.MemberId == memberId);
        if (target is null)
        {
            return WriteStatus.Applied;
        }

        if (target.Role != GroupRole.Participant)
        {
            // An owner's own member is unlinked from the owner, not released as a person.
            await store.ReplacePrincipalAsync(target with { MemberId = null }, null, null, cancellationToken).ConfigureAwait(false);
            return WriteStatus.Applied;
        }

        await store.RemoveAssignmentAsync(target.Id, cancellationToken).ConfigureAwait(false);
        return WriteStatus.Applied;
    }

    // ---------------------------------------------------------------- join link

    /// <summary>What the join link shows: the roster and who is taken.</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The preview, or <see langword="null"/> when the link doesn't work (any more).</returns>
    public async Task<JoinPreview?> PreviewJoinAsync(string joinToken, CancellationToken cancellationToken = default)
    {
        var details = await ByJoinTokenAsync(joinToken, cancellationToken).ConfigureAwait(false);
        return details is null
            ? null
            : GroupViews.ForJoin(details.Group.Document, Taken(details), InvitedOnly(details));
    }

    /// <summary>Takes part through the join link, as an existing member or as a newly added one.</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="request">The member and whether to take part with the account.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the answer.</returns>
    public async Task<(WriteStatus Status, JoinResponse? Response)> JoinAsync(
        string joinToken, Caller caller, JoinRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);

        var details = await ByJoinTokenAsync(joinToken, cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return (WriteStatus.NotFound, null);
        }

        var groupId = details.Group.GroupId;
        if (request.WithAccount && caller.AccountId is null)
        {
            return (WriteStatus.Invalid, null);
        }

        if (request.WithAccount && details.Assignments.Any(a => a.AccountId == caller.AccountId))
        {
            return (WriteStatus.Conflict, null);
        }

        Guid memberId;
        if (request.MemberId is { } picked)
        {
            var member = Group.FromState(details.Group.Document).FindMember(picked);
            if (member is null || member.IsArchived)
            {
                return (WriteStatus.NotFound, null);
            }

            if (Taken(details).Contains(picked))
            {
                return (WriteStatus.Conflict, null);
            }

            memberId = picked;
        }
        else
        {
            var added = await AddMemberAsync(details.Group, request.NewName, cancellationToken).ConfigureAwait(false);
            if (added is null)
            {
                return (WriteStatus.Conflict, null);
            }

            memberId = added.Value;
        }

        var now = clock.GetUtcNow();
        string? deviceKey = null;
        RoleAssignment assignment;
        if (request.WithAccount)
        {
            assignment = Participant(groupId, caller.AccountId!.Value, memberId, RoleAssignmentState.Active, AssignmentRoute.JoinLink, now);
        }
        else
        {
            deviceKey = secrets.NewToken();
            assignment = new RoleAssignment(Guid.CreateVersion7(), groupId, GroupRole.Participant, PrincipalKind.DeviceKey, null, memberId, RoleAssignmentState.Active, AssignmentRoute.JoinLink, now);
        }

        var added2 = await store.AddAssignmentAsync(assignment, deviceKey is null ? null : secrets.Hash(deviceKey), null, cancellationToken).ConfigureAwait(false);
        return added2
            ? (WriteStatus.Applied, new JoinResponse(groupId, memberId, deviceKey))
            : (WriteStatus.Conflict, null);
    }

    // ---------------------------------------------------------------- participant

    /// <summary>What a participant sees.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The view, or <see langword="null"/>.</returns>
    public async Task<ParticipantView?> GetParticipantViewAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is not { MemberId: { } memberId } || !access.Allows(GroupPermissions.ReadOwnView))
        {
            return null;
        }

        var details = await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (details is not null)
        {
            await store.TouchAsync(groupId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }

        return details is null
            ? null
            : GroupViews.ForParticipant(details.Group.Document, details.Group.Version, memberId, GroupPermissionNames.Of(access.Permissions));
    }

    /// <summary>Writes a participant's own entry.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="request">The entry.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome and the new version.</returns>
    public async Task<(WriteStatus Status, long Version)> WriteOwnEntryAsync(
        Guid groupId, Caller caller, OwnEntryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is not { MemberId: { } memberId } || !access.Allows(GroupPermissions.WriteOwnEntry))
        {
            return (WriteStatus.NotFound, 0);
        }

        var wishlists = (request.Wishlists ?? []).Select(w => new EventWishlist(w.EventId, w.Items ?? [])).ToList();
        if (!MemberEntryLimits.AreMet(request.GiftNote, wishlists))
        {
            return (WriteStatus.Invalid, 0);
        }

        var version = await store.WriteEntryAsync(
            groupId, new MemberEntry(memberId, request.GiftNote, wishlists, MemberEntryKeeper.Participant), cancellationToken).ConfigureAwait(false);
        return version is null ? (WriteStatus.NotFound, 0) : (WriteStatus.Applied, version.Value);
    }

    /// <summary>A participant leaves; what they wrote stays in the group.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> LeaveAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.Leave))
        {
            return WriteStatus.NotFound;
        }

        await store.RemoveAssignmentAsync(access.Id, cancellationToken).ConfigureAwait(false);
        return WriteStatus.Applied;
    }

    // ---------------------------------------------------------------- account

    /// <summary>Every group an account has a role in, including invitations waiting for an answer.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The overview.</returns>
    public async Task<MyGroups> OverviewAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var assignments = (await store.ListAssignmentsOfAccountAsync(accountId, cancellationToken).ConfigureAwait(false))
            .Where(a => a.State == RoleAssignmentState.Active || a.ExpiresAt is null || a.ExpiresAt > now)
            .ToList();
        var groups = (await store.GetManyAsync([.. assignments.Select(a => a.GroupId).Distinct()], cancellationToken).ConfigureAwait(false))
            .ToDictionary(g => g.GroupId);

        var list = new List<MyGroup>();
        foreach (var assignment in assignments)
        {
            if (!groups.TryGetValue(assignment.GroupId, out var group))
            {
                continue;
            }

            var member = assignment.MemberId is { } id ? Group.FromState(group.Document).FindMember(id) : null;
            list.Add(new MyGroup(
                group.GroupId,
                group.Document.Name,
                JsonNamingPolicy.CamelCase.ConvertName(assignment.Role.ToString()),
                assignment.State == RoleAssignmentState.Invited,
                assignment.MemberId,
                member?.Name,
                group.Version,
                GroupPermissionNames.Of(assignment.Permissions)));
        }

        return new MyGroups([.. list.OrderBy(g => g.GroupName, StringComparer.CurrentCultureIgnoreCase)]);
    }

    /// <summary>Accepts or declines an email invitation.</summary>
    /// <param name="accountId">The invited account.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="accept">Accept or decline.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> AnswerInvitationAsync(Guid accountId, Guid groupId, bool accept, CancellationToken cancellationToken = default)
    {
        var invitation = await store.FindAssignmentAsync(groupId, accountId, [], cancellationToken).ConfigureAwait(false);
        if (invitation is not { State: RoleAssignmentState.Invited }
            || invitation.ExpiresAt is { } expires && expires <= clock.GetUtcNow())
        {
            return WriteStatus.NotFound;
        }

        if (accept)
        {
            await store.ActivateAsync(invitation.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await store.RemoveAssignmentAsync(invitation.Id, cancellationToken).ConfigureAwait(false);
        }

        return WriteStatus.Applied;
    }

    /// <summary>How many groups an account owns, for the account-deletion screen (FR-043).</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The count.</returns>
    public async Task<int> CountOwnedAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        (await store.ListAssignmentsOfAccountAsync(accountId, cancellationToken).ConfigureAwait(false))
            .Count(a => a.Role == GroupRole.Owner && a.State == RoleAssignmentState.Active);

    /// <summary>
    /// Retention (NFR-010): deletes the groups no account owns that nobody has used for the retention window. A device
    /// that still holds one keeps its own copy; it becomes a group on that device on its next sync.
    /// </summary>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>How many groups were deleted.</returns>
    public async Task<int> PruneInactiveAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(limits.RetentionDays);
        var expired = await store.ListOwnerlessInactiveAsync(cutoff, cancellationToken).ConfigureAwait(false);
        foreach (var groupId in expired)
        {
            await store.DeleteAsync(groupId, cancellationToken).ConfigureAwait(false);
        }

        return expired.Count;
    }

    /// <summary>
    /// Account deletion (ADR-0022, FR-043): owned groups are handed over to an organizer link or deleted, as the user
    /// chose; every other role the account held is removed (what they wrote stays in the groups).
    /// </summary>
    /// <param name="accountId">The account being deleted.</param>
    /// <param name="deleteOwnedGroups">Whether owned groups are deleted rather than handed over.</param>
    /// <param name="cancellationToken">Cancels the cleanup.</param>
    /// <returns>A task.</returns>
    public async Task CleanUpAccountAsync(Guid accountId, bool deleteOwnedGroups, CancellationToken cancellationToken = default)
    {
        foreach (var assignment in await store.ListAssignmentsOfAccountAsync(accountId, cancellationToken).ConfigureAwait(false))
        {
            if (assignment.Role == GroupRole.Owner)
            {
                if (deleteOwnedGroups)
                {
                    await store.DeleteAsync(assignment.GroupId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await HandOverCoreAsync(assignment with { MemberId = null }, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                await store.RemoveAssignmentAsync(assignment.Id, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // ---------------------------------------------------------------- reveal links

    /// <summary>The reveal tokens for every giver of every event's current draw, minted on first ask.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tokens, or <see langword="null"/> without the permission.</returns>
    public async Task<IReadOnlyList<RevealTokenView>?> GetRevealTokensAsync(Guid groupId, Caller caller, CancellationToken cancellationToken = default)
    {
        var access = await AccessAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (access is null || !access.Allows(GroupPermissions.ReadDocument))
        {
            return null;
        }

        var details = await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (details is null)
        {
            return null;
        }

        var givers = Group.FromState(details.Group.Document).Events
            .Where(e => e.CurrentDraw is not null)
            .SelectMany(e => e.CurrentDraw!.Assignments.Select(a => (e.Id, a.GiverMemberId)))
            .ToList();
        var tokens = await store.EnsureRevealTokensAsync(
            groupId,
            givers,
            () =>
            {
                var token = secrets.NewToken();
                return (token, secrets.Hash(token), secrets.Encrypt(token));
            },
            secrets.Decrypt,
            cancellationToken).ConfigureAwait(false);
        return [.. tokens.Select(t => new RevealTokenView(t.EventId, t.GiverMemberId, t.Token))];
    }

    /// <summary>The reveal page behind a token, derived from the document now.</summary>
    /// <param name="revealToken">The token.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, or <see langword="null"/>.</returns>
    public async Task<RevealPayload?> GetRevealAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        var (target, details) = await ByRevealTokenAsync(revealToken, cancellationToken).ConfigureAwait(false);
        if (target is null || details is null)
        {
            return null;
        }

        await store.TouchAsync(details.Group.GroupId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return GroupViews.ForReveal(details.Group.Document, target.EventId, target.GiverMemberId);
    }

    /// <summary>
    /// The entry a reveal-link holder may edit (ADR-0084): their member's gift note and that event's wishlist —
    /// only while no participant keeps that member.
    /// </summary>
    /// <param name="revealToken">The token.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public async Task<RevealEntry?> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        var (target, details) = await ByRevealTokenAsync(revealToken, cancellationToken).ConfigureAwait(false);
        if (target is null || details is null || details.Assignments.Any(a => a.MemberId == target.GiverMemberId))
        {
            return null;
        }

        var group = Group.FromState(details.Group.Document);
        var member = group.FindMember(target.GiverMemberId);
        var occasion = group.FindEvent(target.EventId);
        return member is null || occasion is null
            ? null
            : new RevealEntry(string.IsNullOrEmpty(member.GiftNote) ? null : member.GiftNote, occasion.WishlistFor(member.Id).Items);
    }

    /// <summary>Writes a reveal-link holder's entry, keeping their wishlists for other events.</summary>
    /// <param name="revealToken">The token.</param>
    /// <param name="entry">The gift note and this event's wishlist.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    public async Task<WriteStatus> WriteRevealEntryAsync(string revealToken, RevealEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var (target, details) = await ByRevealTokenAsync(revealToken, cancellationToken).ConfigureAwait(false);
        if (target is null || details is null || details.Assignments.Any(a => a.MemberId == target.GiverMemberId))
        {
            return WriteStatus.NotFound;
        }

        var previous = details.Entries.FirstOrDefault(e => e.MemberId == target.GiverMemberId);
        var wishlists = (previous?.Wishlists ?? [])
            .Where(w => w.EventId != target.EventId)
            .Append(new EventWishlist(target.EventId, entry.Items ?? []))
            .ToList();
        if (!MemberEntryLimits.AreMet(entry.GiftNote, wishlists))
        {
            return WriteStatus.Invalid;
        }

        var version = await store.WriteEntryAsync(
            target.GroupId,
            new MemberEntry(target.GiverMemberId, entry.GiftNote, wishlists, MemberEntryKeeper.RevealLink),
            cancellationToken).ConfigureAwait(false);
        return version is null ? WriteStatus.NotFound : WriteStatus.Applied;
    }

    // ---------------------------------------------------------------- helpers

    private IReadOnlyCollection<string> SecretHashes(Caller caller) =>
        [.. new[] { caller.OrganizerToken, caller.DeviceKey }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => secrets.Hash(s!))];

    private static RoleAssignment Participant(Guid groupId, Guid accountId, Guid memberId, RoleAssignmentState state, AssignmentRoute route, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), groupId, GroupRole.Participant, PrincipalKind.Account, accountId, memberId, state, route, now);

    private async Task<WriteStatus> AddAsync(RoleAssignment assignment, CancellationToken cancellationToken) =>
        await store.AddAssignmentAsync(assignment, null, null, cancellationToken).ConfigureAwait(false)
            ? WriteStatus.Applied
            : WriteStatus.Conflict;

    private async Task<RoleAssignment?> OwnerAsync(Guid groupId, CancellationToken cancellationToken) =>
        (await store.GetAsync(groupId, cancellationToken).ConfigureAwait(false))?.Assignments.FirstOrDefault(a => a.Role == GroupRole.Owner);

    // The owner becomes an organizer link; an account that was also a member keeps taking part as that member.
    private async Task HandOverCoreAsync(RoleAssignment owner, CancellationToken cancellationToken)
    {
        var token = secrets.NewToken();
        var byLink = owner with { PrincipalKind = PrincipalKind.OrganizerLink, AccountId = null, MemberId = null };
        await store.ReplacePrincipalAsync(byLink, secrets.Hash(token), secrets.Encrypt(token), cancellationToken).ConfigureAwait(false);

        if (owner is { AccountId: { } account, MemberId: { } member })
        {
            await store.AddAssignmentAsync(
                Participant(owner.GroupId, account, member, RoleAssignmentState.Active, AssignmentRoute.OwnMember, clock.GetUtcNow()),
                null,
                null,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SetJoinLinkCoreAsync(Guid groupId, bool enabled, CancellationToken cancellationToken)
    {
        var token = secrets.NewToken();
        await store.SetJoinLinkAsync(groupId, enabled, secrets.Hash(token), secrets.Encrypt(token), cancellationToken).ConfigureAwait(false);
    }

    private async Task<GroupLinks> LinksAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var organizer = await store.GetOrganizerSecretCipherAsync(groupId, cancellationToken).ConfigureAwait(false);
        var join = await store.GetJoinLinkCipherAsync(groupId, cancellationToken).ConfigureAwait(false);
        return new GroupLinks(
            organizer is null ? null : secrets.Decrypt(organizer),
            join is null ? null : secrets.Decrypt(join));
    }

    private async Task<OnlineGroupDetails?> ByJoinTokenAsync(string joinToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(joinToken))
        {
            return null;
        }

        var groupId = await store.ResolveJoinLinkAsync(secrets.Hash(joinToken), cancellationToken).ConfigureAwait(false);
        return groupId is null ? null : await store.GetAsync(groupId.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(RevealTarget? Target, OnlineGroupDetails? Details)> ByRevealTokenAsync(string revealToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(revealToken))
        {
            return (null, null);
        }

        var target = await store.ResolveRevealTokenAsync(secrets.Hash(revealToken), cancellationToken).ConfigureAwait(false);
        return target is null
            ? (null, null)
            : (target, await store.GetAsync(target.GroupId, cancellationToken).ConfigureAwait(false));
    }

    private static HashSet<Guid> Taken(OnlineGroupDetails details) =>
        [.. details.Assignments.Where(a => a.MemberId is not null).Select(a => a.MemberId!.Value)];

    // Members reserved by an open invitation and nothing else: the join page says "invited", not "joined" (ST-3).
    private static HashSet<Guid> InvitedOnly(OnlineGroupDetails details) =>
        [.. Taken(details).Except(details.Assignments
            .Where(a => a is { MemberId: not null, State: RoleAssignmentState.Active })
            .Select(a => a.MemberId!.Value))];

    // A person joining under a new name is added to the roster by the server — the one document change a
    // non-organizer can cause. A duplicate name is refused (the person should pick the existing member).
    private async Task<Guid?> AddMemberAsync(OnlineGroup group, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var domain = Group.FromState(group.Document);
        Guid memberId;
        try
        {
            memberId = domain.AddMember(name);
        }
        catch (DuplicateMemberNameException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        var pushed = await store.PushAsync(group.GroupId, group.Version, group.SchemaVersion, domain.ToState(), Guid.Empty, cancellationToken).ConfigureAwait(false);
        return pushed.Status == WriteStatus.Applied ? memberId : null;
    }
}
