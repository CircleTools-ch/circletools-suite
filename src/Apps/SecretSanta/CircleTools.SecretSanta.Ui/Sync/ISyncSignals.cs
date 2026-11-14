// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The single auto-sync signal a listing or detail page needs so it can refresh itself when a
/// background sync cycle (ADR-0044) pulls a co-organizer's edits into the local store — the
/// shared-by-link case where two admins edit the same group (FEAT-013). Kept separate from the full
/// <see cref="AutoSyncService"/> so a page depends only on the notification, and a component test can
/// supply a trivial fake instead of the whole sync graph (mirroring the defensive resolution already
/// used elsewhere in these pages).
/// </summary>
public interface ISyncSignals
{
    /// <summary>
    /// Raised after a background sync cycle drains, on the sync task continuation (off the render
    /// context). A subscriber marshals onto the render context before touching component state.
    /// </summary>
    event Action? SyncCompleted;

    /// <summary>
    /// Raised right after any local change, signed in or out, before a sync runs — so a view showing a
    /// name edited elsewhere on screen (the event back link, BTP-9) can re-read it. A subscriber marshals onto
    /// the render context before touching component state.
    /// </summary>
    event Action? LocalChanged;
}
