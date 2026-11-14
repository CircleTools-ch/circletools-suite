// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// An empty <see cref="ILocalSyncStore"/> for component tests that only need the store to resolve.
/// </summary>
public sealed class EmptyLocalSyncStore : ILocalSyncStore
{
    private readonly string _deviceId = Guid.NewGuid().ToString("N");

    public Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SyncState(_deviceId, []));

    public Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SyncedProfileRecord?> LoadProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SyncedProfileRecord?>(null);

    public Task SaveProfileAsync(SyncedProfileRecord profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SyncedPreferencesRecord?> LoadPreferencesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SyncedPreferencesRecord?>(null);

    public Task SavePreferencesAsync(SyncedPreferencesRecord preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
