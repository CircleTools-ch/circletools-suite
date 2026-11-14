// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// <see cref="ILocalSyncStore"/> over an <see cref="IKeyValueStore"/> (FEAT-004).
/// </summary>
/// <param name="store">The underlying key-value store.</param>
public sealed class LocalSyncStore(IKeyValueStore store) : ILocalSyncStore
{
    /// <summary>The key holding the device sync state (device id, cursors).</summary>
    public const string StateKey = "circletools.secretsanta.sync.state";

    /// <summary>The key holding the per-account profile document (display name, language, sync metadata).</summary>
    public const string ProfileKey = "circletools.secretsanta.sync.profile";

    /// <summary>The key holding the per-account UI preferences document (pinned groups, sync metadata).</summary>
    public const string PreferencesKey = "circletools.secretsanta.sync.preferences";

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

        return new SyncState(Guid.NewGuid().ToString("N"), []);
    }

    /// <inheritdoc />
    public async Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var json = JsonSerializer.Serialize(state, SyncJsonContext.Default.SyncState);
        await WriteOrThrowAsync(StateKey, json, "The sync state could not be saved", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SyncedProfileRecord?> LoadProfileAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.GetAsync(StorageScope.Durable, ProfileKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize(json, SyncJsonContext.Default.SyncedProfileRecord);
    }

    /// <inheritdoc />
    public async Task SaveProfileAsync(SyncedProfileRecord profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var json = JsonSerializer.Serialize(profile, SyncJsonContext.Default.SyncedProfileRecord);
        await WriteOrThrowAsync(ProfileKey, json, "The profile could not be saved", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SyncedPreferencesRecord?> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.GetAsync(StorageScope.Durable, PreferencesKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize(json, SyncJsonContext.Default.SyncedPreferencesRecord);
    }

    /// <inheritdoc />
    public async Task SavePreferencesAsync(SyncedPreferencesRecord preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var json = JsonSerializer.Serialize(preferences, SyncJsonContext.Default.SyncedPreferencesRecord);
        await WriteOrThrowAsync(PreferencesKey, json, "The preferences could not be saved", cancellationToken);
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
