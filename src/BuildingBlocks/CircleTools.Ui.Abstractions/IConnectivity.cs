// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Whether the app currently has a usable network connection, and a signal when that changes
/// (ADR-0044). The seam the silent auto-sync uses to flush queued offline edits the moment
/// connectivity returns.
/// </summary>
/// <remarks>
/// <para>
/// This is one of the platform seams (ADR-0028): the browser reads <c>navigator.onLine</c> and the
/// window <c>online</c>/<c>offline</c> events, the MAUI head wraps
/// <c>Microsoft.Maui.Networking.Connectivity</c>. Everything above it — the <c>AutoSyncService</c> that
/// reacts to <see cref="Changed"/> — is shared feature code that never knows which head it runs on.
/// </para>
/// <para>
/// <strong>It is advisory, never a gate.</strong> Editing must stay fully available regardless of what
/// this reports (NFR-001); a false "online" only means one sync attempt fails and is retried, and a
/// false "offline" only delays a sync until the next trigger. Implementations therefore default to
/// <see langword="true"/> (optimistic) rather than block anything while the real state is unknown.
/// </para>
/// </remarks>
public interface IConnectivity
{
    /// <summary>Whether a network connection is believed to be available. Optimistic until known.</summary>
    bool IsOnline { get; }

    /// <summary>
    /// Raised when connectivity flips, with the new online state. Fired only on an actual change, so a
    /// subscriber can treat a <see langword="true"/> value as "just reconnected".
    /// </summary>
    event Action<bool>? Changed;

    /// <summary>
    /// Starts listening for connectivity changes and reads the current state. Called once, after the
    /// JS runtime / platform APIs are available (from the shell's first render). Best-effort: a failure
    /// to reach the platform API leaves <see cref="IsOnline"/> at its optimistic default rather than throwing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initialization.</param>
    /// <returns>A task that completes once listening has started.</returns>
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}
