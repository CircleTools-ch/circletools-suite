using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>
/// Keeps this device's online groups in step with the Secret-Santa group store (ADR-0086, FEAT-027) — one service
/// for every role:
/// <list type="bullet">
/// <item>a group this device <strong>organizes</strong>: a dirty copy is pushed first, then the server copy is read
/// (members' own entries merged in, roles, links, permissions) — push before pull, so an offline edit is never
/// overwritten by a pull (review finding C4);</item>
/// <item>a group this device <strong>takes part in</strong>: a pending own entry is sent, then the participant view
/// is read;</item>
/// <item>signed in, the account's groups are discovered on the server, so they appear on every device.</item>
/// </list>
/// Every call is best-effort: an unreachable server leaves local state as it was, to be retried (NFR-008).
/// </summary>
/// <param name="store">This device's group records.</param>
/// <param name="api">The group store's API.</param>
public sealed class OnlineGroupSync(IGroupRecordStore store, IOnlineGroupApi api)
{
    // ---------------------------------------------------------------- place

    /// <summary>
    /// Makes a device group online. Signed in, the account owns it; signed out, an organizer link does, kept on this
    /// device.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="signedInAccount">The signed-in account, if any.</param>
    /// <param name="joinEnabled">Whether to turn the join link on right away.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> MakeOnlineAsync(Guid groupId, Guid? signedInAccount, bool joinEnabled, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(groupId, cancellationToken);
        if (record is not { Place: GroupPlace.Device, Document: { } document })
        {
            return ApiResult.Failed(new ApiError(ApiFailure.Conflict, "The group is not on this device only."));
        }

        // The creator key is minted and kept before the call, so a retry after a lost response re-presents it and
        // gets the same links back instead of a conflict.
        var creatorKey = signedInAccount is null ? Guid.NewGuid().ToString("N") : null;
        var created = await api.CreateAsync(
            new CreateOnlineGroupRequest(GroupDocument.CurrentSchemaVersion, Element(document), creatorKey, joinEnabled), cancellationToken);
        if (!created.IsSuccess)
        {
            return ApiResult.Failed(created.Error!);
        }

        var access = signedInAccount is { } account
            ? new OnlineAccess(AccessPrincipal.Account, "owner", [], AccountId: account, JoinToken: created.Value!.JoinToken)
            : new OnlineAccess(AccessPrincipal.OrganizerLink, "owner", [], Secret: created.Value!.OrganizerToken, CreatorKey: creatorKey, JoinToken: created.Value.JoinToken);
        await UpdateAsync(groupId, r => r with { Place = GroupPlace.Online, ServerVersion = 1, Dirty = false, Access = access }, cancellationToken);

        await SyncGroupAsync(groupId, cancellationToken);
        return ApiResult.Ok();
    }

    /// <summary>
    /// Keeps the group only on this device: the online copy is deleted (everyone else loses access) and the local
    /// document stays.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> KeepOnDeviceAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(groupId, cancellationToken);
        if (record is not { Place: GroupPlace.Online, Document: not null, Access: { } access } || !access.Can(OnlinePermissions.DeleteGroup))
        {
            return ApiResult.Failed(new ApiError(ApiFailure.Forbidden));
        }

        var deleted = await api.DeleteAsync(groupId, access.Credentials, cancellationToken);
        if (!deleted.IsSuccess && deleted.Error!.Failure != ApiFailure.NotFound)
        {
            return deleted;
        }

        await UpdateAsync(groupId, r => r with { Place = GroupPlace.Device, View = null, Access = null, ServerVersion = 0, Dirty = false, SelfMaintained = null, Roles = null }, cancellationToken);
        return ApiResult.Ok();
    }

    /// <summary>
    /// The account was deleted (FR-043): its groups can no longer be reached from this device. A group it organized
    /// stays here as a device group, so nothing typed is lost (the deletion page promises that data on the person's own
    /// devices stays untouched); a group it only took part in leaves the device with the account.
    /// </summary>
    /// <param name="accountId">The deleted account.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    public async Task ForgetAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var held = (await store.LoadAsync(cancellationToken)).Records
            .Where(r => r.Access is { Principal: AccessPrincipal.Account } access && access.AccountId == accountId)
            .ToList();
        foreach (var record in held)
        {
            if (record.Document is not null)
            {
                await LoseAccessAsync(record.Id, cancellationToken);
            }
            else
            {
                await RemoveLocallyAsync(record.Id, cancellationToken);
            }
        }

        // Its records waiting in Parked go the same way: an organized one stays as a device group.
        var document = await store.LoadAsync(cancellationToken);
        await store.SaveAsync(
            document with
            {
                Parked = [.. (document.Parked ?? [])
                    .Where(r => Holder(r) != accountId || r.Document is not null)
                    .Select(r => Holder(r) == accountId ? r with { Place = GroupPlace.Device, View = null, Access = null, ServerVersion = 0, Dirty = false, SelfMaintained = null, Roles = null } : r)],
            },
            cancellationToken);
    }

    // ---------------------------------------------------------------- sync

    /// <summary>
    /// One sync cycle: queued removals, then (signed in) discovery of the account's groups, then every online group
    /// visible on this device.
    /// </summary>
    /// <param name="signedInAccount">The signed-in account, if any.</param>
    /// <param name="cancellationToken">Cancels the cycle.</param>
    /// <returns>A task.</returns>
    public async Task SyncAsync(Guid? signedInAccount, CancellationToken cancellationToken = default)
    {
        // Local and first, so a sign-in or sign-out puts the right holder's records in play even offline.
        if (Arrange(await store.LoadAsync(cancellationToken), signedInAccount) is { } arranged)
        {
            await store.SaveAsync(arranged, cancellationToken);
        }

        await SendRemovalsAsync(signedInAccount, cancellationToken);

        if (signedInAccount is { } account)
        {
            await DiscoverAsync(account, cancellationToken);
        }

        var document = await store.LoadAsync(cancellationToken);
        foreach (var record in document.Records.Where(r => r.Place == GroupPlace.Online && r.IsVisibleTo(signedInAccount)).ToList())
        {
            await SyncGroupAsync(record.Id, cancellationToken);
        }
    }

    /// <summary>Syncs one online group, in whatever role this device holds there.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    public async Task SyncGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(groupId, cancellationToken);
        if (record is not { Place: GroupPlace.Online, Access: { } access })
        {
            return;
        }

        if (record.Document is not null)
        {
            await SyncOrganizedAsync(record, access, cancellationToken);
        }
        else
        {
            await SyncTakingPartAsync(record, access, cancellationToken);
        }
    }

    private async Task SyncOrganizedAsync(GroupRecord record, OnlineAccess access, CancellationToken cancellationToken)
    {
        if (record.Dirty && access.Can(OnlinePermissions.WriteDocument))
        {
            var pushed = await api.PushAsync(
                record.Id,
                new PushOnlineGroupRequest(record.ServerVersion, GroupDocument.CurrentSchemaVersion, Element(record.Document!)),
                access.Credentials,
                cancellationToken);
            if (pushed.IsSuccess)
            {
                var answer = pushed.Value!;
                await UpdateAsync(
                    record.Id,
                    current => SameDocument(current.Document, record.Document)
                        ? current with { Document = answer.Rewritten ? Read(answer.Document) ?? current.Document : current.Document, ServerVersion = answer.Version, Dirty = false }
                        : current with { ServerVersion = answer.Version },
                    cancellationToken);
            }
            else if (pushed.Error!.Failure == ApiFailure.NotFound)
            {
                await LoseAccessAsync(record.Id, cancellationToken);
                return;
            }
            else
            {
                return;
            }
        }

        var read = await api.GetAsync(record.Id, access.Credentials, cancellationToken);
        if (!read.IsSuccess)
        {
            if (read.Error!.Failure == ApiFailure.NotFound)
            {
                await LoseAccessAsync(record.Id, cancellationToken);
            }

            return;
        }

        var view = read.Value!;
        await UpdateAsync(
            record.Id,
            current =>
            {
                var adopt = !current.Dirty && view.Version > current.ServerVersion;
                return current with
                {
                    Document = adopt ? Read(view.Document) ?? current.Document : current.Document,
                    ServerVersion = adopt ? view.Version : current.ServerVersion,
                    SelfMaintained = view.SelfMaintained,
                    Roles = view.Roles,
                    Access = current.Access! with
                    {
                        Permissions = view.Permissions,
                        Role = OwnRole(view, current.Access!) ?? current.Access!.Role,
                        Secret = current.Access!.Principal == AccessPrincipal.OrganizerLink ? view.Links.OrganizerToken ?? current.Access!.Secret : current.Access!.Secret,
                        JoinToken = view.Links.JoinToken,
                    },
                };
            },
            cancellationToken);

        // An organizer who is also in the roster sees their own assignment like any participant (ADR-0086).
        if (view.Permissions.Contains(OnlinePermissions.ReadOwnView))
        {
            var own = await api.GetParticipantViewAsync(record.Id, access.Credentials, cancellationToken);
            if (own.IsSuccess)
            {
                await UpdateAsync(record.Id, r => r with { View = own.Value }, cancellationToken);
            }
        }
    }

    private async Task SyncTakingPartAsync(GroupRecord record, OnlineAccess access, CancellationToken cancellationToken)
    {
        if (record.PendingEntry is { } entry)
        {
            var written = await api.WriteOwnEntryAsync(record.Id, entry, access.Credentials, cancellationToken);
            if (written.IsSuccess || written.Error!.Failure is ApiFailure.Validation)
            {
                // Accepted, or refused for good (out of bounds): either way it is no longer pending.
                await UpdateAsync(record.Id, r => SameEntry(r.PendingEntry, entry) ? r with { PendingEntry = null } : r, cancellationToken);
            }
        }

        var read = await api.GetParticipantViewAsync(record.Id, access.Credentials, cancellationToken);
        if (read.IsSuccess)
        {
            var view = read.Value!;
            await UpdateAsync(
                record.Id,
                r => r with { View = view, ServerVersion = view.Version, Access = r.Access! with { Permissions = view.Permissions, MemberId = view.MemberId } },
                cancellationToken);
        }
        else if (read.Error!.Failure == ApiFailure.NotFound)
        {
            // Released, or the group was deleted: nothing of it is this device's any more.
            await RemoveLocallyAsync(record.Id, cancellationToken);
        }
    }

    // A group this device organized is gone from the server (deleted elsewhere, or ownership moved): the document
    // stays here as a device group, so nothing the organizer typed is lost.
    private Task LoseAccessAsync(Guid groupId, CancellationToken cancellationToken) =>
        UpdateAsync(groupId, r => r with { Place = GroupPlace.Device, View = null, Access = null, ServerVersion = 0, Dirty = false, SelfMaintained = null, Roles = null }, cancellationToken);

    private async Task DiscoverAsync(Guid account, CancellationToken cancellationToken)
    {
        var mine = await api.GetMyGroupsAsync(cancellationToken);
        if (!mine.IsSuccess)
        {
            return;
        }

        var groups = mine.Value!.Groups;
        var document = await store.LoadAsync(cancellationToken);

        // Groups this account held here but no longer has a role in (released, deleted, handed over) go.
        var active = groups.Where(g => !g.Invited).ToDictionary(g => g.GroupId);
        bool Stale(GroupRecord r) => r.Place == GroupPlace.Online && Holder(r) == account && !active.ContainsKey(r.Id);
        var records = document.Records.Where(r => !Stale(r)).ToList();
        var parked = (document.Parked ?? []).Where(r => !Stale(r)).ToList();

        // A group gets this account's own record even when someone else's is here (another account, signed out; or
        // the device's link or key): the arrangement below decides which one is in play (ST-1).
        foreach (var group in active.Values.Where(g => !records.Concat(parked).Any(r => r.Id == g.GroupId && Holder(r) == account)))
        {
            var access = new OnlineAccess(AccessPrincipal.Account, group.Role, group.Permissions, AccountId: account, MemberId: group.MemberId);
            var organizes = access.Can(OnlinePermissions.ReadDocument);
            records.Add(new GroupRecord(group.GroupId, GroupPlace.Online, null, null, 0, Dirty: false, Access: access) with
            {
                // A placeholder until the group's own sync fills it: organizers get the document, participants the view.
                Document = organizes ? EmptyDocument(group) : null,
            });
        }

        var discovered = document with { Records = records, Parked = parked, Invitations = [.. groups.Where(g => g.Invited)] };
        await store.SaveAsync(Arrange(discovered, account) ?? discovered, cancellationToken);
    }

    // The document is read right after discovery; until then an empty roster with the right id and name stands in,
    // and a version of 0 makes the first read adopt the server copy.
    private static GroupState EmptyDocument(MyGroup group) =>
        new(group.GroupId, group.GroupName, null, null, [], [], [], Events: []);

    private async Task SendRemovalsAsync(Guid? signedInAccount, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var sent = new List<PendingRemoval>();
        foreach (var removal in document.Removals.Where(r => r.Access.IsVisibleTo(signedInAccount)))
        {
            var result = removal.Leave
                ? await api.LeaveAsync(removal.GroupId, removal.Access.Credentials, cancellationToken)
                : await api.DeleteAsync(removal.GroupId, removal.Access.Credentials, cancellationToken);
            if (result.IsSuccess || result.Error!.Failure == ApiFailure.NotFound)
            {
                sent.Add(removal);
            }
        }

        if (sent.Count > 0)
        {
            // Matched by group and kind, not by record equality: the queue is read afresh here, and its access carries
            // a permission list that is a different instance, so equality never matched and every sent removal stayed
            // queued — the delete went out again on every later cycle (SS-5, 2026-10-28).
            var latest = await store.LoadAsync(cancellationToken);
            await store.SaveAsync(
                latest with { Removals = [.. latest.Removals.Where(r => !sent.Exists(s => s.GroupId == r.GroupId && s.Leave == r.Leave))] },
                cancellationToken);
        }
    }

    // ---------------------------------------------------------------- taking part

    /// <summary>Takes part through a join link and adds the group to this device.</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="request">Which member, and whether with the account.</param>
    /// <param name="signedInAccount">The signed-in account, required for a join with the account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome and the answer.</returns>
    public async Task<ApiResult<JoinResponse>> JoinAsync(string joinToken, JoinRequest request, Guid? signedInAccount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var joined = await api.JoinAsync(joinToken, request, cancellationToken);
        if (!joined.IsSuccess)
        {
            return joined;
        }

        var answer = joined.Value!;
        var access = request.WithAccount
            ? new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: signedInAccount, MemberId: answer.MemberId)
            : new OnlineAccess(AccessPrincipal.DeviceKey, "participant", [], MemberId: answer.MemberId, Secret: answer.DeviceKey);

        var document = await store.LoadAsync(cancellationToken);
        await store.SaveAsync(
            Put(document, new GroupRecord(answer.GroupId, GroupPlace.Online, null, null, 0, Dirty: false, Access: access)),
            cancellationToken);

        await SyncGroupAsync(answer.GroupId, cancellationToken);
        return joined;
    }

    /// <summary>Writes this device's own entry in a group it takes part in: kept locally, sent now if possible.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    public async Task WriteOwnEntryAsync(Guid groupId, OwnEntryRequest entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await UpdateAsync(groupId, r => r.Access?.Can(OnlinePermissions.WriteOwnEntry) == true ? r with { PendingEntry = entry } : r, cancellationToken);
        await SyncGroupAsync(groupId, cancellationToken);
    }

    /// <summary>Leaves a group: removed here at once, the server is told now or on the next sync.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="signedInAccount">The signed-in account, if any.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    public async Task LeaveAsync(Guid groupId, Guid? signedInAccount, CancellationToken cancellationToken = default)
    {
        var document = await store.LoadAsync(cancellationToken);
        var record = document.Records.FirstOrDefault(r => r.Id == groupId);
        if (record is not { Access: { } access } || !access.Can(OnlinePermissions.Leave))
        {
            return;
        }

        await store.SaveAsync(
            document with
            {
                Records = [.. document.Records.Where(r => r.Id != groupId)],
                Removals = [.. document.Removals.Where(r => r.GroupId != groupId), new PendingRemoval(groupId, access, Leave: true)],
            },
            cancellationToken);
        await SendRemovalsAsync(signedInAccount, cancellationToken);
    }

    /// <summary>Answers an email invitation, then syncs so an accepted group appears.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="accept">Accept or decline.</param>
    /// <param name="signedInAccount">The signed-in account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> AnswerInvitationAsync(Guid groupId, bool accept, Guid signedInAccount, CancellationToken cancellationToken = default)
    {
        var answered = await api.AnswerInvitationAsync(groupId, accept, cancellationToken);
        if (answered.IsSuccess)
        {
            await DiscoverAsync(signedInAccount, cancellationToken);
            await SyncGroupAsync(groupId, cancellationToken);
        }

        return answered;
    }

    // ---------------------------------------------------------------- organizer actions

    /// <summary>Turns the join link on or off (optionally replacing it) and keeps the link on the record.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="enabled">On or off.</param>
    /// <param name="rotate">Whether to replace it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome and the links.</returns>
    public async Task<ApiResult<GroupLinks>> SetJoinLinkAsync(Guid groupId, bool enabled, bool rotate = false, CancellationToken cancellationToken = default)
    {
        var access = (await FindAsync(groupId, cancellationToken))?.Access;
        if (access is null)
        {
            return ApiResult.Failed<GroupLinks>(new ApiError(ApiFailure.NotFound));
        }

        var links = await api.SetJoinLinkAsync(groupId, new JoinLinkRequest(enabled, rotate), access.Credentials, cancellationToken);
        if (links.IsSuccess)
        {
            await UpdateAsync(groupId, r => r with { Access = r.Access! with { JoinToken = links.Value!.JoinToken } }, cancellationToken);
        }

        return links;
    }

    /// <summary>
    /// Opens a management link on this device (co-organizing without an account, ADR-0086 decision D1): the group
    /// is read with the link's token and kept here, organized by that link.
    /// </summary>
    /// <param name="groupId">The group the link names.</param>
    /// <param name="organizerToken">The link's token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the link works and the group is now on this device.</returns>
    public async Task<bool> AdoptOrganizerLinkAsync(Guid groupId, string organizerToken, CancellationToken cancellationToken = default)
    {
        var credentials = new OnlineCredentials(OrganizerToken: organizerToken);
        var read = await api.GetAsync(groupId, credentials, cancellationToken);
        if (!read.IsSuccess || Read(read.Value!.Document) is not { } document)
        {
            return false;
        }

        var view = read.Value;
        var access = new OnlineAccess(AccessPrincipal.OrganizerLink, "owner", view.Permissions, Secret: organizerToken, JoinToken: view.Links.JoinToken);
        var stored = await store.LoadAsync(cancellationToken);
        await store.SaveAsync(
            Put(stored, new GroupRecord(groupId, GroupPlace.Online, document, null, view.Version, Dirty: false, access, SelfMaintained: view.SelfMaintained, Roles: view.Roles)),
            cancellationToken);
        return true;
    }

    /// <summary>Replaces the management link of a group organized by its link; this device keeps the new one.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome and the links.</returns>
    public async Task<ApiResult<GroupLinks>> RotateOrganizerLinkAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var access = (await FindAsync(groupId, cancellationToken))?.Access;
        if (access is not { Principal: AccessPrincipal.OrganizerLink })
        {
            return ApiResult.Failed<GroupLinks>(new ApiError(ApiFailure.Conflict));
        }

        var links = await api.RotateOrganizerLinkAsync(groupId, access.Credentials, cancellationToken);
        if (links.IsSuccess && links.Value!.OrganizerToken is { } token)
        {
            await UpdateAsync(groupId, r => r with { Access = r.Access! with { Secret = token } }, cancellationToken);
        }

        return links;
    }

    /// <summary>The signed-in account takes over a group this device organizes by its link.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="signedInAccount">The account.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> TakeOverAsync(Guid groupId, Guid signedInAccount, CancellationToken cancellationToken = default)
    {
        var access = (await FindAsync(groupId, cancellationToken))?.Access;
        if (access is not { Principal: AccessPrincipal.OrganizerLink, Secret: { } token })
        {
            return ApiResult.Failed(new ApiError(ApiFailure.Conflict));
        }

        var taken = await api.TakeOverAsync(groupId, token, cancellationToken);
        if (taken.IsSuccess)
        {
            await UpdateAsync(
                groupId,
                r => r with { Access = r.Access! with { Principal = AccessPrincipal.Account, AccountId = signedInAccount, Secret = null, CreatorKey = null } },
                cancellationToken);
            await SyncGroupAsync(groupId, cancellationToken);
        }

        return taken;
    }

    /// <summary>The owning account hands the group back to an organizer link, which this device keeps.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> HandOverAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var handed = await api.HandOverAsync(groupId, cancellationToken);
        if (!handed.IsSuccess)
        {
            return ApiResult.Failed(handed.Error!);
        }

        await UpdateAsync(
            groupId,
            r => r with { Access = r.Access! with { Principal = AccessPrincipal.OrganizerLink, AccountId = null, Secret = handed.Value!.OrganizerToken } },
            cancellationToken);
        await SyncGroupAsync(groupId, cancellationToken);
        return ApiResult.Ok();
    }

    /// <summary>Brings a person in as a member, then re-reads who has a role.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="request">Who and how.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> AddParticipantAsync(Guid groupId, AddParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var access = (await FindAsync(groupId, cancellationToken))?.Access;
        if (access is null)
        {
            return ApiResult.Failed(new ApiError(ApiFailure.NotFound));
        }

        // The participant points at a member of the server copy, so a member just added here is pushed first.
        await SyncGroupAsync(groupId, cancellationToken);
        var added = await api.AddParticipantAsync(groupId, request, access.Credentials, cancellationToken);
        if (added.IsSuccess)
        {
            await SyncGroupAsync(groupId, cancellationToken);
        }

        return added;
    }

    /// <summary>Releases a participant (or withdraws an invitation), then re-reads who has a role.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outcome.</returns>
    public async Task<ApiResult> ReleaseAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var access = (await FindAsync(groupId, cancellationToken))?.Access;
        if (access is null)
        {
            return ApiResult.Failed(new ApiError(ApiFailure.NotFound));
        }

        var released = await api.ReleaseAsync(groupId, memberId, access.Credentials, cancellationToken);
        if (released.IsSuccess)
        {
            await SyncGroupAsync(groupId, cancellationToken);
        }

        return released;
    }

    /// <summary>The reveal tokens for the group's current draws, to build short reveal links (ADR-0082).</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tokens, or a failure (offline: the share page falls back to snapshot links).</returns>
    public async Task<ApiResult<IReadOnlyList<RevealTokenView>>> GetRevealTokensAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(groupId, cancellationToken);
        if (record is not { Place: GroupPlace.Online, Access: { } access })
        {
            return ApiResult.Failed<IReadOnlyList<RevealTokenView>>(new ApiError(ApiFailure.NotFound));
        }

        // The tokens point at the server copy, so an unsent edit (a fresh draw) is pushed first.
        await SyncGroupAsync(groupId, cancellationToken);
        return await api.GetRevealTokensAsync(groupId, access.Credentials, cancellationToken);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<GroupRecord?> FindAsync(Guid groupId, CancellationToken cancellationToken) =>
        (await store.LoadAsync(cancellationToken)).Records.FirstOrDefault(r => r.Id == groupId);

    // Reads the latest document right before writing, so a change made in between (a local edit) is not lost.
    private async Task UpdateAsync(Guid groupId, Func<GroupRecord, GroupRecord> change, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var records = document.Records.ToList();
        var index = records.FindIndex(r => r.Id == groupId);
        if (index < 0)
        {
            return;
        }

        records[index] = change(records[index]);
        await store.SaveAsync(document with { Records = records }, cancellationToken);
    }

    private async Task RemoveLocallyAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        await store.SaveAsync(document with { Records = [.. document.Records.Where(r => r.Id != groupId)] }, cancellationToken);
    }

    // Who a record belongs to on this device: an account, or (null) the device itself — a device group, an organizer
    // link or a device key, shown whoever is signed in.
    private static Guid? Holder(GroupRecord record) =>
        record.Access is { Principal: AccessPrincipal.Account } access ? access.AccountId : null;

    // Pages and sync find a group by its id, so one record per group is in play and the others wait in Parked (ST-1).
    // Of the records the signed-in person may see (that account's and the device's), the one that organizes is in
    // play, else the account's; a group only other accounts hold keeps the record it had. Null when nothing moves.
    private static GroupRecordsDocument? Arrange(GroupRecordsDocument document, Guid? signedInAccount)
    {
        var pool = document.Records.Concat(document.Parked ?? []).ToList();
        var inPlay = pool
            .GroupBy(r => r.Id)
            .Select(g => g
                .Where(r => Holder(r) is null || Holder(r) == signedInAccount)
                .OrderBy(r => r.Document is null)
                .ThenBy(r => Holder(r) is null)
                .FirstOrDefault()
                ?? document.Records.FirstOrDefault(r => r.Id == g.Key)
                ?? g.First())
            .ToList();
        if (inPlay.SequenceEqual(document.Records, ReferenceEqualityComparer.Instance))
        {
            return null;
        }

        return document with { Records = inPlay, Parked = [.. pool.Where(r => !inPlay.Contains(r, ReferenceEqualityComparer.Instance))] };
    }

    // A new record for a group takes the place of the one in play. One of the same holder is replaced; another
    // holder's waits in Parked instead of being dropped (ST-1).
    private static GroupRecordsDocument Put(GroupRecordsDocument document, GroupRecord record)
    {
        bool Other(GroupRecord r) => r.Id != record.Id || Holder(r) != Holder(record);
        return document with
        {
            Records = [.. document.Records.Where(r => r.Id != record.Id), record],
            Parked = [.. (document.Parked ?? []).Where(Other), .. document.Records.Where(r => r.Id == record.Id && Other(r))],
        };
    }

    private static string? OwnRole(OrganizerGroupView view, OnlineAccess access) =>
        access.Principal == AccessPrincipal.OrganizerLink
            ? view.Roles.FirstOrDefault(r => r.Principal == "organizerLink")?.Role
            : null;

    // Records come back from storage as new instances, so "unchanged since the push" compares content, not references.
    private static bool SameDocument(GroupState? a, GroupState? b) =>
        a is not null && b is not null && GroupDocument.Serialize(a) == GroupDocument.Serialize(b);

    // An entry written again while the first one was on its way stays pending; the one sent is cleared.
    private static bool SameEntry(OwnEntryRequest? a, OwnEntryRequest b) =>
        a is not null
        && JsonSerializer.Serialize(a, OnlineJsonContext.Default.OwnEntryRequest) == JsonSerializer.Serialize(b, OnlineJsonContext.Default.OwnEntryRequest);

    private static JsonElement Element(GroupState document)
    {
        using var parsed = JsonDocument.Parse(GroupDocument.Serialize(document));
        return parsed.RootElement.Clone();
    }

    private static GroupState? Read(JsonElement element) =>
        GroupDocument.TryDeserialize(element.GetRawText(), out var document) ? document : null;
}
