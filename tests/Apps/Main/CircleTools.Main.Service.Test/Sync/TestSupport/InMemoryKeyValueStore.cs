// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// A device's local storage, in memory (FEAT-004 harness). Each <see cref="SyncClient"/> owns one, so
/// two devices never share a device id, a cursor or a group collection — the separation the harness
/// depends on. Stands in for the browser's <c>localStorage</c>; <see cref="RefuseWrites"/> models a
/// store that is blocked or full.
/// </summary>
public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<(StorageScope Scope, string Key), string> _values = [];

    /// <summary>When set, every write is refused (returns <see langword="false"/>), as a full store does.</summary>
    public bool RefuseWrites { get; set; }

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_values.TryGetValue((scope, key), out var value) ? value : null);

    /// <inheritdoc />
    public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default)
    {
        if (RefuseWrites)
        {
            return ValueTask.FromResult(false);
        }

        _values[(scope, key)] = value;
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
    {
        _values.Remove((scope, key));
        return ValueTask.CompletedTask;
    }
}
