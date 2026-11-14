// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The one thing the Settings screen needs from the reminder engine (MNF-8): reschedule after a toggle
/// change. Kept as a small seam (like <see cref="ISyncSignals"/>) so the Settings page depends only on
/// this, and a component test can supply a trivial fake instead of the whole <see cref="ReminderScheduler"/>
/// graph (which pulls in the full sync service).
/// </summary>
public interface IReminderRescheduler
{
    /// <summary>Recomputes and reschedules the device-local reminders from the current data and settings.</summary>
    /// <param name="cancellationToken">Cancels the reconcile.</param>
    /// <returns>A task that completes when the schedule is up to date.</returns>
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}
