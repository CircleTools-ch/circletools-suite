using System.Text.Json;
using AppSuite.SecretSanta.Core.Persistence;
using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// <see cref="ILocalSyncStore"/> over an <see cref="IKeyValueStore"/> (FEAT-004). Groups live under
/// the same key <see cref="GroupRepository"/> has always used, so signing in migrates the existing
/// data in place rather than starting a second, parallel store.
/// </summary>
/// <param name="store">The underlying key-value store.</param>
public sealed class LocalSyncStore(IKeyValueStore store) : ILocalSyncStore
{
    /// <summary>The key holding the group collection — the same one the pre-sync store used (migrated in place).</summary>
    public const string GroupsKey = GroupRepository.StorageKey;

    /// <summary>The key holding the device sync state (device id, cursors, pending tombstones).</summary>
    public const string StateKey = "appsuite.secretsanta.sync.state";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SyncedGroupRecord>> LoadGroupsAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.GetAsync(StorageScope.Durable, GroupsKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);

        // Schema v1 was a bare array of GroupState; schema v2 is a wrapper object. A device that
        // stored groups before M3 has the array, and is lifted into records here (version 1, no
        // account scope, not dirty) so nothing is lost and nothing is spuriously queued for push.
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            var legacy = document.RootElement.Deserialize(GroupStateJsonContext.Default.ListGroupState) ?? [];
            return legacy
                .Select(state => new SyncedGroupRecord(state, Version: 1, AccountScope: null, Dirty: false))
                .ToList();
        }

        var parsed = document.RootElement.Deserialize(SyncJsonContext.Default.SyncGroupDocument);
        return parsed?.Records ?? [];
    }

    /// <inheritdoc />
    public async Task SaveGroupsAsync(
        IReadOnlyList<SyncedGroupRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        var document = new SyncGroupDocument(SyncGroupDocument.CurrentSchemaVersion, records);
        var json = JsonSerializer.Serialize(document, SyncJsonContext.Default.SyncGroupDocument);
        await WriteOrThrowAsync(GroupsKey, json, "The groups could not be saved", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.GetAsync(StorageScope.Durable, StateKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(json))
        {
            var parsed = JsonSerializer.Deserialize(json, SyncJsonContext.Default.SyncState);
            if (parsed is not null && !string.IsNullOrEmpty(parsed.DeviceId))
            {
                return parsed;
            }
        }

        return new SyncState(Guid.NewGuid().ToString("N"), [], []);
    }

    /// <inheritdoc />
    public async Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, SyncJsonContext.Default.SyncState);
        await WriteOrThrowAsync(StateKey, json, "The sync state could not be saved", cancellationToken);
    }

    private async Task WriteOrThrowAsync(string key, string json, string message, CancellationToken cancellationToken)
    {
        var stored = await store.SetAsync(StorageScope.Durable, key, json, cancellationToken);
        if (!stored)
        {
            throw new StorageUnavailableException($"{message}: local storage is unavailable or full.");
        }
    }
}
