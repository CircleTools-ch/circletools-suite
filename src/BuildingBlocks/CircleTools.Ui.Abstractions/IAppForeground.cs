// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Whether the app is in the foreground (visible to the user), and when that changes — the seam behind
/// the resume and foreground-poll sync triggers (ADR-0044 amendment 2026-10-15). The browser reports page
/// visibility; a device head reports its window being resumed or stopped.
/// </summary>
public interface IAppForeground
{
    /// <summary>Gets a value indicating whether the app is currently in the foreground.</summary>
    bool IsForeground { get; }

    /// <summary>Raised with the new state when the app comes to the foreground or leaves it.</summary>
    event Action<bool>? Changed;

    /// <summary>Starts watching. Idempotent; best-effort (a head that cannot tell stays "foreground").</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes once watching has started.</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>An <see cref="IAppForeground"/> that never changes: always in the foreground. The default for a host that cannot tell.</summary>
public sealed class AlwaysForeground : IAppForeground
{
    /// <inheritdoc />
    public bool IsForeground => true;

    /// <inheritdoc />
    public event Action<bool>? Changed
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
