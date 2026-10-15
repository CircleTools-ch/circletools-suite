using System.Security.Cryptography;
using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>How joining a shared-by-link group via a join link ended (ADR-0058, FEAT-013).</summary>
public enum JoinStatus
{
    /// <summary>The member was claimed and the group added to this device.</summary>
    Joined,

    /// <summary>The chosen member was already claimed by someone else — the first-claim lock held.</summary>
    AlreadyClaimed,

    /// <summary>The join link did not resolve (wrong, disabled, or the group is gone).</summary>
    NotFound,

    /// <summary>The join could not complete (e.g. the server was unreachable).</summary>
    Failed,
}

/// <summary>The outcome of <see cref="SharedSyncService.JoinAsync"/>.</summary>
/// <param name="Status">How the join ended.</param>
/// <param name="GroupId">The joined group's id, when <see cref="Status"/> is <see cref="JoinStatus.Joined"/>.</param>
public sealed record JoinResult(JoinStatus Status, Guid GroupId);

/// <summary>
/// The client engine for the shared-by-link storage tier (ADR-0058, FEAT-013). It owns the
/// out-of-cycle transitions — sharing a local group by link, making a shared group local again,
/// rotating the admin link — and the per-group reconcile that keeps a shared group's one aggregate in
/// step with the server (last-write-wins, mirroring <see cref="SyncService"/> but token-addressed and
/// one group at a time).
/// </summary>
/// <remarks>
/// A shared-by-link group is not account-scoped, so this runs signed in <em>or out</em>. The group
/// body is the same opaque <see cref="Core.Persistence.GroupState"/> memento the account sync carries;
/// only the addressing (a share token) and the authorization (the token's capability) differ.
/// </remarks>
/// <param name="local">The shared local-store gateway.</param>
/// <param name="api">The anonymous shared-group API port.</param>
/// <param name="accountApi">The authorized shared-group API port (claim-admin / release-admin).</param>
public sealed class SharedSyncService(ILocalSyncStore local, ISharedGroupApi api, ISharedGroupAccountApi accountApi)
{
    /// <summary>Uploads a local group into shared-by-link storage and records its links (the upgrade).</summary>
    /// <param name="groupId">The group to share.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The minted links, or <see langword="null"/> when the group is unknown or the server is unreachable.</returns>
    public async Task<MintedLinks?> ShareByLinkAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0)
        {
            return null;
        }

        var record = records[index];
        if (record is { Mode: StorageMode.SharedByLink, Tokens.AdminToken: { } admin, Tokens.JoinToken: { } join })
        {
            return new MintedLinks(groupId, admin, join); // already shared — idempotent
        }

        // The creator key proves, on this and any retry, that we are the group's creator rather than
        // someone who merely learned the group id from a join link (ADR-0058 security note). Mint it once
        // and persist it before the create call, so a lost-response retry re-presents the same key and the
        // server hands the links back rather than refusing.
        var creatorKey = record.Tokens?.CreatorKey;
        if (creatorKey is null)
        {
            creatorKey = NewCreatorKey();
            record = record with { Tokens = new SharedGroupTokens(null, null, creatorKey) };
            records[index] = record;
            await local.SaveGroupsAsync(records, cancellationToken);
        }

        var body = JsonSerializer.SerializeToElement(record.State, SyncJsonContext.Default.GroupState);
        var result = await api.CreateAsync(new CreateSharedGroupRequest(groupId, record.Version, body, creatorKey), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return null;
        }

        var links = result.Value;
        var sentVersion = record.Version;
        await UpdateAsync(
            groupId,
            current => current with
            {
                Mode = StorageMode.SharedByLink,
                Tokens = new SharedGroupTokens(links.AdminToken, links.JoinToken, creatorKey),

                // Clean only if nothing changed since the uploaded copy; an edit made meanwhile is pushed next.
                Dirty = current.Version != sentVersion,
            },
            cancellationToken);
        return links;
    }

    /// <summary>Makes a shared-by-link group local-only again (the downgrade): removes the server copy, keeps the local one.</summary>
    /// <param name="groupId">The group to bring back to this device.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task MakeLocalAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0 || records[index].Mode != StorageMode.SharedByLink)
        {
            return;
        }

        var record = records[index];

        // An admin-link holder deletes the online copy (owner-less delete, NFR-010); a join-only holder
        // simply detaches locally. Either way the local copy is kept — this is not a delete.
        if (record.Tokens?.AdminToken is { } adminToken)
        {
            await api.DeleteAsync(adminToken, cancellationToken);
        }

        // Tokens (including the creator key) are dropped: the server copy is gone, so a later re-share is
        // a fresh group as far as the server is concerned and mints a new creator key.
        await UpdateAsync(groupId, current => current with { Mode = StorageMode.OnThisDevice, Tokens = null, Dirty = false }, cancellationToken);
    }

    /// <summary>Disables the current admin link and mints a fresh one (ADR-0058).</summary>
    /// <param name="groupId">The shared group whose admin link to rotate.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new admin token, or <see langword="null"/> when the device holds no admin link or the server is unreachable.</returns>
    public async Task<string?> RotateAdminAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0 || records[index].Tokens?.AdminToken is not { } adminToken)
        {
            return null;
        }

        var result = await api.RotateAdminAsync(adminToken, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return null;
        }

        var fresh = result.Value.AdminToken;
        await UpdateAsync(
            groupId,
            current => current with { Tokens = (current.Tokens ?? new SharedGroupTokens(null, null)) with { AdminToken = fresh } },
            cancellationToken);
        return fresh;
    }

    /// <summary>
    /// Joins a shared-by-link group through a join link (ADR-0058): claims the chosen member (first
    /// claim wins), then adds the group to this device with that claimed identity so the participant
    /// sees the group and their own assignment. The claim locks the member so no one else can take it.
    /// </summary>
    /// <param name="joinToken">The join token from the link.</param>
    /// <param name="memberId">The member the joiner is claiming ("this is me").</param>
    /// <param name="kind">Whether the claim is device-only or bound to an account.</param>
    /// <param name="deviceHandle">This device's stable handle (from the sync state), for a device claim.</param>
    /// <param name="accountId">The signed-in account, for an account claim.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The join outcome.</returns>
    public async Task<JoinResult> JoinAsync(
        string joinToken,
        Guid memberId,
        PersonClaimKind kind,
        string? deviceHandle,
        Guid? accountId,
        CancellationToken cancellationToken = default)
    {
        var get = await api.GetAsync(joinToken, cancellationToken);
        if (!get.IsSuccess || get.Value is null)
        {
            return new JoinResult(JoinStatus.NotFound, Guid.Empty);
        }

        var claim = await api.ClaimPersonAsync(
            joinToken, new ClaimPersonRequest(memberId, kind, deviceHandle, accountId), cancellationToken);
        if (!claim.IsSuccess || claim.Value is null)
        {
            return new JoinResult(JoinStatus.Failed, Guid.Empty);
        }

        if (claim.Value.AlreadyClaimed)
        {
            return new JoinResult(JoinStatus.AlreadyClaimed, Guid.Empty);
        }

        var state = get.Value.Body.Deserialize(SyncJsonContext.Default.GroupState);
        if (state is null)
        {
            return new JoinResult(JoinStatus.Failed, Guid.Empty);
        }

        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var joined = new SyncedGroupRecord(
            state, get.Value.Version, AccountScope: null, Dirty: false,
            Mode: StorageMode.SharedByLink,
            Tokens: new SharedGroupTokens(AdminToken: null, JoinToken: joinToken),
            ClaimedMemberId: memberId);

        var index = records.FindIndex(r => r.State.Id == state.Id);
        if (index >= 0)
        {
            records[index] = joined;
        }
        else
        {
            records.Add(joined);
        }

        await local.SaveGroupsAsync(records, cancellationToken);
        return new JoinResult(JoinStatus.Joined, state.Id);
    }

    /// <summary>Claims admin of a shared-by-link group into the signed-in account (ADR-0058): Shared by link → My account.</summary>
    /// <param name="accountId">The signed-in account taking admin; recorded on the group (ADR-0078).</param>
    /// <param name="groupId">The shared group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the account now owns the group.</returns>
    public async Task<bool> ClaimAdminToAccountAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0 || records[index].Tokens?.AdminToken is not { } adminToken)
        {
            return false;
        }

        var result = await accountApi.ClaimAdminAsync(adminToken, cancellationToken);
        if (!result.IsSuccess)
        {
            return false;
        }

        // The account now owns it; the admin link is retired. Drop the (now-dead) admin token, keep the
        // join token (participants are unaffected), and mark the group account-owned — by this account, so
        // it is hidden while signed out and from any other account on this device (ADR-0078).
        await UpdateAsync(
            groupId,
            current => current with
            {
                Mode = StorageMode.Account,
                Tokens = (current.Tokens ?? new SharedGroupTokens(null, null)) with { AdminToken = null },
                AdminAccount = accountId,
            },
            cancellationToken);
        return true;
    }

    /// <summary>Releases an account-owned group back to shared-by-link (ADR-0058): My account → Shared by link.</summary>
    /// <param name="groupId">The shared group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the group was released and a fresh admin link stored.</returns>
    public async Task<bool> ReleaseToLinkAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0)
        {
            return false;
        }

        var result = await accountApi.ReleaseAdminAsync(groupId, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return false;
        }

        var admin = result.Value.AdminToken;
        await UpdateAsync(
            groupId,
            current => current with
            {
                Mode = StorageMode.SharedByLink,
                Tokens = new SharedGroupTokens(admin, current.Tokens?.JoinToken),
                AdminAccount = null,
            },
            cancellationToken);
        return true;
    }

    /// <summary>
    /// Adopts an admin link on this device (ADR-0058): a co-organizer opening the admin link gets the
    /// group with admin capability. A retired admin link is not adopted — the caller shows who took over.
    /// </summary>
    /// <param name="adminToken">The admin token from the link.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The adopted group's id, or <see langword="null"/> when the link is retired or unresolvable.</returns>
    public async Task<Guid?> AdoptAdminAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        var get = await api.GetAsync(adminToken, cancellationToken);
        if (!get.IsSuccess || get.Value is null || get.Value.AdminRetired)
        {
            return null;
        }

        var state = get.Value.Body.Deserialize(SyncJsonContext.Default.GroupState);
        if (state is null)
        {
            return null;
        }

        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == state.Id);
        var existingJoin = index >= 0 ? records[index].Tokens?.JoinToken : null;
        var claimed = index >= 0 ? records[index].ClaimedMemberId : null;
        var adopted = new SyncedGroupRecord(
            state, get.Value.Version, AccountScope: null, Dirty: false,
            Mode: StorageMode.SharedByLink,
            Tokens: new SharedGroupTokens(adminToken, existingJoin),
            ClaimedMemberId: claimed);

        if (index >= 0)
        {
            records[index] = adopted;
        }
        else
        {
            records.Add(adopted);
        }

        await local.SaveGroupsAsync(records, cancellationToken);
        return state.Id;
    }

    /// <summary>Reads which members of a shared-by-link group are already claimed (for the admin's roster).</summary>
    /// <param name="groupId">The shared group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The claimed member ids, or empty when the group is not shared or the server is unreachable.</returns>
    public async Task<IReadOnlyList<Guid>> GetClaimedMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var record = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
        var token = record?.Tokens?.AdminToken ?? record?.Tokens?.JoinToken;
        if (token is null)
        {
            return [];
        }

        var result = await api.GetClaimsAsync(token, cancellationToken);
        return result is { IsSuccess: true, Value: not null } ? result.Value.MemberIds : [];
    }

    /// <summary>Releases a claimed member so it can be claimed again — admin only, the sole recovery path (ADR-0058).</summary>
    /// <param name="groupId">The shared group.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the member was released.</returns>
    public async Task<bool> ReleasePersonAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default)
    {
        var record = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
        if (record?.Tokens?.AdminToken is not { } adminToken)
        {
            return false;
        }

        var result = await api.ReleasePersonAsync(adminToken, memberId, cancellationToken);
        return result.IsSuccess;
    }

    /// <summary>Runs one reconcile for a single shared group: pull if the server is newer, push a dirty local edit.</summary>
    /// <remarks>
    /// Every write re-reads the record and changes only what this reconcile decided, never the copy read
    /// before the network call: an edit made while the request was in flight (a member added, a draw run)
    /// used to be overwritten by that stale copy and never uploaded. A pull never replaces an unsynced
    /// local edit at least as new as the server's copy; it is pushed instead, as in <see cref="SyncService"/>.
    /// </remarks>
    /// <param name="groupId">The shared group to reconcile.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task SyncGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var record = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
        if (record is null)
        {
            return;
        }

        // A claimed (My account) shared group is owner-authorized: reconcile it by group id + bearer
        // (D2). This covers both the device that claimed it (which kept a join token) and a second
        // device that discovered it via MergeOwnedAsync. An account-store group is also "My account" but
        // lives in the account sync store, so it never comes this way (ADR-0078 Amendment 3).
        if (record.IsAdminClaimedShared())
        {
            await SyncOwnedAsync(groupId, cancellationToken);
            return;
        }

        if (record is not { Mode: StorageMode.SharedByLink, Tokens: { } tokens })
        {
            return;
        }

        var token = tokens.AdminToken ?? tokens.JoinToken;
        if (token is null)
        {
            return;
        }

        var get = await api.GetAsync(token, cancellationToken);
        if (!get.IsSuccess)
        {
            // The online copy is gone (deleted or expired, NFR-010): fall back to on-this-device and
            // keep the local copy. An unreachable server leaves everything untouched (NFR-001).
            if (get.Error?.Failure == ApiFailure.NotFound)
            {
                await UpdateAsync(groupId, current => current with { Mode = StorageMode.OnThisDevice, Tokens = null }, cancellationToken);
            }

            return;
        }

        var view = get.Value!;

        // An account has claimed admin: this device's admin link is retired. Keep the join link (so a
        // participant keeps access) and stop trying to push through the dead admin link.
        if (view.AdminRetired && record.Tokens?.AdminToken is not null)
        {
            await UpdateAsync(
                groupId,
                current => current.Tokens is { AdminToken: not null } held ? current with { Tokens = held with { AdminToken = null } } : current,
                cancellationToken);
        }

        if (await PullIfNewerAsync(groupId, view, cancellationToken))
        {
            return;
        }

        // Only an admin-link holder pushes the whole group; a participant's own edits travel a narrower
        // per-member path (a later slice). Server last-write-wins arbitrates.
        var pending = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
        if (pending is { Dirty: true, Tokens.AdminToken: { } admin })
        {
            var body = JsonSerializer.SerializeToElement(pending.State, SyncJsonContext.Default.GroupState);
            var push = await api.PushAsync(admin, new PushSharedGroupRequest(pending.Version, body), cancellationToken);
            if (push.IsSuccess && push.Value is not null)
            {
                await MarkPushedAsync(groupId, pending.Version, push.Value.Version, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Discovers the signed-in account's owned shared groups on this device (D2): a group claimed on
    /// another device is fetched and added locally as a My-account group, so the owner manages it here
    /// too. Existing records are left for <see cref="SyncGroupAsync"/> to reconcile — except that a
    /// held "My account" record not yet attributed to an account (written before ADR-0078) is attributed
    /// to this one as its admin account, so the auto-claim never uploads it into the account store too.
    /// A held group this account claimed admin of that is no longer listed is settled: removed when it was
    /// deleted online, handed back to its link (or kept on this device) when another owner or a release
    /// took it (ADR-0078).
    /// </summary>
    /// <param name="accountId">The signed-in account whose owned groups are listed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the owned list was read (whether or not anything changed).</returns>
    public async Task<bool> MergeOwnedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var result = await accountApi.ListOwnedAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return false;
        }

        // A group this account claimed admin of but no longer owns online: deleted on another device, or
        // handed back to a link there. Ask which (network) before touching the local list, so this device
        // follows instead of keeping a stale "My account" copy for good (ADR-0078). Only this account's
        // admin-claimed groups: an account-store group (AccountScope) follows its own tombstones, and another
        // account's group is not ours to settle.
        var listed = result.Value.Groups.Select(g => g.GroupId).ToHashSet();
        var verdicts = new Dictionary<Guid, ApiFailure>();
        foreach (var groupId in (await local.LoadGroupsAsync(cancellationToken))
            .Where(r => r is { Mode: StorageMode.Account, AccountScope: null } && r.AdminAccount == accountId
                && !listed.Contains(r.State.Id))
            .Select(r => r.State.Id)
            .ToList())
        {
            if ((await accountApi.GetOwnedAsync(groupId, cancellationToken)).Error?.Failure is { } failure)
            {
                verdicts[groupId] = failure;
            }
        }

        // Now apply everything to the list as it is now, with no network call before the save, so an edit
        // made meanwhile is not overwritten by an older copy.
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var syncState = await local.LoadStateAsync(cancellationToken);
        var deleting = (syncState.OwnedDeletes ?? []).Select(d => d.GroupId).ToHashSet();
        var changed = false;
        foreach (var owned in result.Value.Groups)
        {
            // Deleted on this device, its owner delete not sent yet: never bring it back (ADR-0078).
            if (deleting.Contains(owned.GroupId))
            {
                continue;
            }

            var state = owned.Body.Deserialize(SyncJsonContext.Default.GroupState);
            var index = records.FindIndex(r => r.State.Id == owned.GroupId);
            if (index >= 0)
            {
                var held = records[index];

                // Held here only through its join link, by a device signed into the account that owns it:
                // follow the claim and become the owner's copy (ADR-0078 Amendment 3). The join-only copy
                // could never send an edit, so the server body replaces it.
                if (held.IsJoinOnly() && held.AccountScope is null && state is not null)
                {
                    records[index] = held with
                    {
                        State = state,
                        Version = owned.Version,
                        Dirty = false,
                        Mode = StorageMode.Account,
                        AdminAccount = accountId,
                        PendingClaimAccount = null,
                        Tokens = held.Tokens is { } tokens ? tokens with { AdminToken = null } : null,
                    };
                    changed = true;
                }

                // Otherwise already known — SyncGroupAsync reconciles it. Backfill the owner on a legacy record.
                else if (held is { Mode: StorageMode.Account, AccountScope: null, AdminAccount: null })
                {
                    records[index] = held with { AdminAccount = accountId, PendingClaimAccount = null };
                    changed = true;
                }

                continue;
            }

            if (state is null)
            {
                continue;
            }

            records.Add(new SyncedGroupRecord(
                state, owned.Version, AccountScope: null, Dirty: false,
                Mode: StorageMode.Account, Tokens: null, AdminAccount: accountId));
            changed = true;
        }

        foreach (var (groupId, failure) in verdicts)
        {
            var index = records.FindIndex(r => r.State.Id == groupId);
            if (index < 0)
            {
                continue;
            }

            switch (failure)
            {
                case ApiFailure.NotFound:
                    // Deleted online: deleting means gone, on every device.
                    records.RemoveAt(index);
                    changed = true;
                    break;
                case ApiFailure.Forbidden:
                    // Still online but not this account's any more: keep the local copy, and keep following
                    // the group by its join link when this device has one.
                    var record = records[index];
                    records[index] = record.Tokens is { JoinToken: not null } tokens
                        ? record with { Mode = StorageMode.SharedByLink, AdminAccount = null, Tokens = tokens with { AdminToken = null } }
                        : record with { Mode = StorageMode.OnThisDevice, AdminAccount = null, Tokens = null };
                    changed = true;
                    break;
                default:
                    // Unreachable, or another failure: leave it for the next cycle.
                    break;
            }
        }

        if (changed)
        {
            await local.SaveGroupsAsync(records, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Sends the owner deletes queued by deleting "My account" groups on this device (ADR-0078), so the
    /// online copy goes and the account's other devices stop discovering the group. Only the deletes for
    /// <paramref name="accountId"/> (or with no account recorded) are sent.
    /// </summary>
    /// <remarks>
    /// Best-effort and offline-safe: a delete that cannot reach the server stays queued for the next cycle.
    /// One the server settles — deleted, already gone, or refused because the caller does not own the group
    /// (any more) — is dropped: there is nothing more this device can do about it.
    /// </remarks>
    /// <param name="accountId">The signed-in account.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    public async Task SendOwnedDeletesAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var state = await local.LoadStateAsync(cancellationToken);
        if (state.OwnedDeletes is not { Count: > 0 } queued)
        {
            return;
        }

        var settled = new HashSet<Guid>();
        foreach (var pending in queued.Where(d => d.AccountId is null || d.AccountId == accountId))
        {
            var result = await accountApi.DeleteOwnedAsync(pending.GroupId, cancellationToken);
            if (result.IsSuccess || result.Error!.Failure is ApiFailure.Forbidden or ApiFailure.NotFound)
            {
                settled.Add(pending.GroupId);
            }
        }

        if (settled.Count == 0)
        {
            return;
        }

        // Re-read before writing: a delete made while the calls were in flight must stay queued.
        var latest = await local.LoadStateAsync(cancellationToken);
        var remaining = (latest.OwnedDeletes ?? []).Where(d => !settled.Contains(d.GroupId)).ToList();
        await local.SaveStateAsync(latest with { OwnedDeletes = remaining }, cancellationToken);
    }

    // Reconcile a claimed (My account) shared group via the owner path (D2): the owner has no admin
    // token, so they read and write it by group id, authorized by their bearer. Writes re-read the record,
    // like SyncGroupAsync.
    private async Task SyncOwnedAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var get = await accountApi.GetOwnedAsync(groupId, cancellationToken);
        if (!get.IsSuccess || get.Value is null)
        {
            return; // not the owner any more, or the server is unreachable - leave the local copy as-is
        }

        var view = get.Value;

        // Keep the owner's join link current, so Sharing & sync can offer it on every device of the
        // owner - including one that discovered the group and never held a token (ADR-0078 Amendment 3).
        if (view.JoinToken is { } joinToken)
        {
            await UpdateAsync(
                groupId,
                current => current.Tokens?.JoinToken == joinToken
                    ? current
                    : current with { Tokens = (current.Tokens ?? new SharedGroupTokens(null, null)) with { AdminToken = null, JoinToken = joinToken } },
                cancellationToken);
        }

        if (await PullIfNewerAsync(groupId, view, cancellationToken))
        {
            return;
        }

        var pending = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
        if (pending is { Dirty: true })
        {
            var body = JsonSerializer.SerializeToElement(pending.State, SyncJsonContext.Default.GroupState);
            var push = await accountApi.PushOwnedAsync(groupId, new PushSharedGroupRequest(pending.Version, body), cancellationToken);
            if (push.IsSuccess && push.Value is not null)
            {
                await MarkPushedAsync(groupId, pending.Version, push.Value.Version, cancellationToken);
            }
        }
    }

    // Applies a newer server copy, unless the local copy holds an unsynced edit at least as new: that one is
    // pushed instead (last-write-wins, as in SyncService). Returns whether the server copy was applied.
    private async Task<bool> PullIfNewerAsync(Guid groupId, SharedGroupView view, CancellationToken cancellationToken)
    {
        var state = view.Body.Deserialize(SyncJsonContext.Default.GroupState);
        if (state is null)
        {
            return false;
        }

        var applied = false;
        await UpdateAsync(
            groupId,
            current =>
            {
                if (view.Version <= current.Version)
                {
                    return current;
                }

                applied = true;
                return current with { State = state, Version = view.Version, Dirty = false };
            },
            cancellationToken);
        return applied;
    }

    // Marks a push as accepted. Clean only if nothing changed locally since the copy that was sent; an edit
    // made meanwhile stays dirty for the next reconcile, on top of the server's version.
    private Task MarkPushedAsync(Guid groupId, long sentVersion, long serverVersion, CancellationToken cancellationToken) =>
        UpdateAsync(
            groupId,
            current => current.Version == sentVersion
                ? current with { Version = serverVersion, Dirty = false }
                : current with { Version = Math.Max(current.Version, serverVersion + 1) },
            cancellationToken);

    // Re-reads the group list and applies one change to the record as it is now, so nothing written since
    // the caller last read it (a local edit, another reconcile) is lost.
    private async Task UpdateAsync(Guid groupId, Func<SyncedGroupRecord, SyncedGroupRecord> change, CancellationToken cancellationToken)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0)
        {
            return;
        }

        var updated = change(records[index]);
        if (ReferenceEquals(updated, records[index]))
        {
            return;
        }

        records[index] = updated;
        await local.SaveGroupsAsync(records, cancellationToken);
    }

    /// <summary>Mints a fresh, unguessable creator key (256 bits, URL-safe) for a first share (ADR-0058).</summary>
    private static string NewCreatorKey() =>
        Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
