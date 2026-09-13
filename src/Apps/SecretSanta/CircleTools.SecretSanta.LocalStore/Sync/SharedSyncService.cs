using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

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
public sealed class SharedSyncService(ILocalSyncStore local, ISharedGroupApi api)
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

    /// <summary>Runs one reconcile for a single shared-by-link group: pull if the server is newer, push a dirty local edit.</summary>
    /// <param name="groupId">The shared group to reconcile.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task SyncGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0 || records[index] is not { Mode: StorageMode.SharedByLink, Tokens: { } tokens })
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
}
