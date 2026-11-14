// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// One device in the two-client harness (FEAT-004): the real Secret-Santa account sync — a
/// <see cref="LocalSyncStore"/> over its own storage and the <see cref="SyncService"/> that runs a cycle — bound
/// to a single account's authorized transport. The document it carries is the per-account preferences (pinned
/// groups, ADR-0041); groups have their own sync (ADR-0086). The only test double is the in-memory stand-in for
/// the browser's storage.
/// </summary>
public sealed class SyncClient
{
    private readonly Guid _accountId;
    private readonly InMemoryKeyValueStore _storage = new();
    private readonly LocalSyncStore _local;
    private readonly SyncService _sync;

    /// <summary>Creates a device for <paramref name="accountId"/> over <paramref name="api"/>.</summary>
    /// <param name="accountId">The account this device syncs under.</param>
    /// <param name="api">The transport to the host (optionally decorated to model an interruption).</param>
    public SyncClient(Guid accountId, ISyncApi api)
    {
        _accountId = accountId;
        _local = new LocalSyncStore(_storage);
        _sync = new SyncService(_local, api);
    }

    /// <summary>Whether the device's local storage refuses writes, as a full browser store would (ADR-0029).</summary>
    public bool StorageRefusesWrites
    {
        get => _storage.RefuseWrites;
        set => _storage.RefuseWrites = value;
    }

    /// <summary>Runs one sync cycle for the account — the single mechanism behind every trigger.</summary>
    /// <param name="cancellationToken">Cancels the cycle.</param>
    /// <returns>How the cycle ended and what moved.</returns>
    public Task<SyncOutcome> SyncAsync(CancellationToken cancellationToken = default) =>
        _sync.SyncNowAsync(_accountId, cancellationToken);

    /// <summary>Pins a group, as the app's bookmark does: the next version, marked dirty.</summary>
    /// <param name="groupId">The group to pin.</param>
    /// <returns>A task.</returns>
    public async Task PinAsync(Guid groupId)
    {
        var current = await _local.LoadPreferencesAsync();
        var pinned = (current?.State.PinnedGroupIds ?? []).Append(groupId).ToList();
        await _local.SavePreferencesAsync(new SyncedPreferencesRecord(
            new PreferencesState(pinned), (current?.Version ?? 0) + 1, _accountId, Dirty: true));
    }

    /// <summary>The groups the device has pinned.</summary>
    /// <returns>The pinned group ids.</returns>
    public async Task<IReadOnlyList<Guid>> PinnedAsync() =>
        (await _local.LoadPreferencesAsync())?.State.PinnedGroupIds ?? [];

    /// <summary>The device's preferences record, with its sync metadata.</summary>
    /// <returns>The record, or <see langword="null"/>.</returns>
    public Task<SyncedPreferencesRecord?> PreferencesAsync() => _local.LoadPreferencesAsync();

    /// <summary>The cursor the device has reached on the account (its highest applied server sequence).</summary>
    /// <returns>The account cursor, or zero if it has never synced.</returns>
    public async Task<long> CursorAsync() => (await _local.LoadStateAsync()).CursorFor(_accountId);
}
