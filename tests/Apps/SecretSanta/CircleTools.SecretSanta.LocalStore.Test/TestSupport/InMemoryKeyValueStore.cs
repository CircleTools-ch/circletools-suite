// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test.TestSupport;

/// <summary>
/// Hand-written in-memory <see cref="IKeyValueStore"/> used to exercise <c>GroupRepository</c>
/// without touching the browser or the file system.
/// </summary>
public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<(StorageScope Scope, string Key), string> _values = [];

    /// <summary>
    /// When set, every write is refused — the store stays empty and <c>SetAsync</c> returns
    /// <see langword="false"/>, as a browser with storage blocked or full does.
    /// </summary>
    public bool RefuseWrites { get; set; }

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(
        StorageScope scope,
        string key,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_values.TryGetValue((scope, key), out var value) ? value : null);

    /// <inheritdoc />
    public ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
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
