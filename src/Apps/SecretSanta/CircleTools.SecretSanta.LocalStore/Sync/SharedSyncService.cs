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

        var body = JsonSerializer.SerializeToElement(record.State, SyncJsonContext.Default.GroupState);
        var result = await api.CreateAsync(new CreateSharedGroupRequest(groupId, record.Version, body), cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return null;
        }

        var links = result.Value;
        records[index] = record with
        {
            Mode = StorageMode.SharedByLink,
            Tokens = new SharedGroupTokens(links.AdminToken, links.JoinToken),
            Dirty = false,
        };
        await local.SaveGroupsAsync(records, cancellationToken);
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

        records[index] = record with { Mode = StorageMode.OnThisDevice, Tokens = null, Dirty = false };
        await local.SaveGroupsAsync(records, cancellationToken);
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

        var record = records[index];
        records[index] = record with { Tokens = record.Tokens! with { AdminToken = result.Value.AdminToken } };
        await local.SaveGroupsAsync(records, cancellationToken);
        return result.Value.AdminToken;
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
    /// <param name="groupId">The shared group.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when the account now owns the group.</returns>
    public async Task<bool> ClaimAdminToAccountAsync(Guid groupId, CancellationToken cancellationToken = default)
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

        var record = records[index];
        // The account now owns it; the admin link is retired. Drop the (now-dead) admin token, keep the
        // join token (participants are unaffected), and mark the group account-owned.
        records[index] = record with { Mode = StorageMode.Account, Tokens = record.Tokens! with { AdminToken = null } };
        await local.SaveGroupsAsync(records, cancellationToken);
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

        var record = records[index];
        records[index] = record with
        {
            Mode = StorageMode.SharedByLink,
            Tokens = new SharedGroupTokens(result.Value.AdminToken, record.Tokens?.JoinToken),
        };
        await local.SaveGroupsAsync(records, cancellationToken);
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
    /// <param name="groupId">The shared group to reconcile.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task SyncGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0)
        {
            return;
        }

        // A claimed (My account) shared group is owner-authorized: reconcile it by group id + bearer
        // (D2). This covers both the device that claimed it (which kept a join token) and a second
        // device that discovered it via MergeOwnedAsync (no tokens at all).
        if (records[index].Mode == StorageMode.Account)
        {
            await SyncOwnedAsync(records, index, groupId, cancellationToken);
            return;
        }

        if (records[index] is not { Mode: StorageMode.SharedByLink, Tokens: { } tokens })
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
                records[index] = records[index] with { Mode = StorageMode.OnThisDevice, Tokens = null };
                await local.SaveGroupsAsync(records, cancellationToken);
            }

            return;
        }

        var view = get.Value!;
        var record = records[index];

        // An account has claimed admin: this device's admin link is retired. Keep the join link (so a
        // participant keeps access) and stop trying to push through the dead admin link.
        if (view.AdminRetired && record.Tokens?.AdminToken is not null)
        {
            record = record with { Tokens = record.Tokens with { AdminToken = null } };
            records[index] = record;
            await local.SaveGroupsAsync(records, cancellationToken);
        }

        if (view.Version > record.Version)
        {
            var state = view.Body.Deserialize(SyncJsonContext.Default.GroupState);
            if (state is not null)
            {
                records[index] = record with { State = state, Version = view.Version, Dirty = false };
                await local.SaveGroupsAsync(records, cancellationToken);
            }
        }
        else if (record is { Dirty: true, Tokens.AdminToken: { } admin })
        {
            // Only an admin-link holder pushes the whole group; a participant's own edits travel a
            // narrower per-member path (a later slice). Server last-write-wins arbitrates.
            var body = JsonSerializer.SerializeToElement(record.State, SyncJsonContext.Default.GroupState);
            var push = await api.PushAsync(admin, new PushSharedGroupRequest(record.Version, body), cancellationToken);
            if (push.IsSuccess && push.Value is not null)
            {
                records[index] = record with { Version = push.Value.Version, Dirty = false };
                await local.SaveGroupsAsync(records, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Discovers the signed-in account's owned shared groups on this device (D2): a group claimed on
    /// another device is fetched and added locally as a My-account group, so the owner manages it here
    /// too. Existing records are left for <see cref="SyncGroupAsync"/> to reconcile.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task MergeOwnedAsync(CancellationToken cancellationToken = default)
    {
        var result = await accountApi.ListOwnedAsync(cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return;
        }

        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var changed = false;
        foreach (var owned in result.Value.Groups)
        {
            if (records.Any(r => r.State.Id == owned.GroupId))
            {
                continue; // already known — SyncGroupAsync reconciles it
            }

            var state = owned.Body.Deserialize(SyncJsonContext.Default.GroupState);
            if (state is null)
            {
                continue;
            }

            records.Add(new SyncedGroupRecord(
                state, owned.Version, AccountScope: null, Dirty: false,
                Mode: StorageMode.Account, Tokens: null));
            changed = true;
        }

        if (changed)
        {
            await local.SaveGroupsAsync(records, cancellationToken);
        }
    }

    // Reconcile a claimed (My account) shared group via the owner path (D2): the owner has no admin
    // token, so they read and write it by group id, authorized by their bearer.
    private async Task SyncOwnedAsync(List<SyncedGroupRecord> records, int index, Guid groupId, CancellationToken cancellationToken)
    {
        var record = records[index];
        var get = await accountApi.GetOwnedAsync(groupId, cancellationToken);
        if (!get.IsSuccess || get.Value is null)
        {
            return; // not the owner any more, or the server is unreachable — leave the local copy as-is
        }

        var view = get.Value;
        if (view.Version > record.Version)
        {
            var state = view.Body.Deserialize(SyncJsonContext.Default.GroupState);
            if (state is not null)
            {
                records[index] = record with { State = state, Version = view.Version, Dirty = false };
                await local.SaveGroupsAsync(records, cancellationToken);
            }
        }
        else if (record.Dirty)
        {
            var body = JsonSerializer.SerializeToElement(record.State, SyncJsonContext.Default.GroupState);
            var push = await accountApi.PushOwnedAsync(groupId, new PushSharedGroupRequest(record.Version, body), cancellationToken);
            if (push.IsSuccess && push.Value is not null)
            {
                records[index] = record with { Version = push.Value.Version, Dirty = false };
                await local.SaveGroupsAsync(records, cancellationToken);
            }
        }
    }
}
