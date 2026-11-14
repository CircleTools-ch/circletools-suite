// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Turns the local occasions and the user's chosen reminder kinds into scheduled device notifications
/// (MNF-8, FR-046, ADR-0060). It reconciles the schedule on app start and after any local change, so a date
/// change, a completed draw, a filled wishlist or a deleted event never leaves a stale reminder — cancelling
/// only future reminders that changed, never one already shown (R12). A no-op on the web (the scheduler
/// reports unsupported there).
/// </summary>
/// <remarks>
/// A notification body is built from the event name and status only; it never contains an assignment —
/// who drew whom must not leak through a reminder (NFR-002).
/// </remarks>
/// <param name="sender">Dispatches the local queries the reminders are computed from.</param>
/// <param name="auth">The current principal, for the signed-in account (the "my wishlist" reminder).</param>
/// <param name="scheduler">The platform notification scheduler seam.</param>
/// <param name="preferences">Which reminder kinds are switched on.</param>
/// <param name="localizer">Localized notification titles and bodies.</param>
/// <param name="time">The clock, for "now" when deciding which reminders are still in the future.</param>
/// <param name="autoSync">The sync service, whose LocalChanged event drives a reschedule after any edit.</param>
/// <param name="foreground">
/// Whether the app is visible: coming back (e.g. from the system settings) applies reminders the user asked for
/// while notifications were blocked (R15).
/// </param>
/// <param name="records">This device's group records, for which member is the signed-in person (ADR-0086).</param>
public sealed class ReminderScheduler(
    ISender sender,
    AuthenticationStateProvider auth,
    INotificationScheduler scheduler,
    NotificationPreferences preferences,
    IStringLocalizer<AppStrings> localizer,
    TimeProvider time,
    AutoSyncService autoSync,
    IAppForeground foreground,
    IGroupRecordStore records) : IReminderRescheduler, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(750);

    private CancellationTokenSource? _debounce;
    private bool _initialized;

    /// <summary>
    /// Starts keeping the schedule in sync: reconciles once now, and again (debounced) on every local
    /// change. Called once from the shell's first render. Idempotent.
    /// </summary>
    /// <returns>A task that completes once the initial reconcile has been requested.</returns>
    public async ValueTask InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        autoSync.LocalChanged += RequestReconcile;
        autoSync.SyncCompleted += RequestReconcile; // a pulled change (co-organizer's edit) reschedules too
        foreground.Changed += OnForegroundChanged;
        await ApplyWantedIfAllowedAsync();
        await ReconcileAsync();
    }

    /// <summary>
    /// Switches on the reminder kinds the user asked for while notifications were blocked, once they are allowed
    /// (R15, 2026-10-20 device test) — e.g. "Remind me" after a draw, then allowing notifications in the system
    /// settings. A no-op while they are still blocked or nothing is waiting.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> when at least one kind was switched on.</returns>
    public async Task<bool> ApplyWantedIfAllowedAsync(CancellationToken cancellationToken = default)
    {
        if (!await scheduler.IsSupportedAsync(cancellationToken)
            || (await preferences.GetWantedAsync(cancellationToken)).Count == 0
            || await scheduler.GetPermissionAsync(cancellationToken) != NotificationAccess.Granted)
        {
            return false;
        }

        return (await preferences.ApplyWantedAsync(cancellationToken)).Count > 0;
    }

    private void OnForegroundChanged(bool isForeground)
    {
        if (isForeground)
        {
            _ = ApplyOnResumeAsync();
        }
    }

    private async Task ApplyOnResumeAsync()
    {
        try
        {
            if (await ApplyWantedIfAllowedAsync())
            {
                RequestReconcile();
            }
        }
        catch
        {
            // Reminders are a convenience; the next resume tries again (NFR-001).
        }
    }

    /// <summary>Requests a reconcile soon, coalescing a burst of changes into one pass.</summary>
    public void RequestReconcile()
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        var cts = new CancellationTokenSource();
        _debounce = cts;
        _ = RunAfterDelayAsync(cts.Token);
    }

    private async Task RunAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DebounceDelay, cancellationToken);
            await ReconcileAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later change; the newer request will run.
        }
        catch
        {
            // Reminders are a convenience; a failed reconcile is retried on the next change (NFR-001).
        }
    }

    /// <summary>Recomputes and reschedules all reminders from the current local data.</summary>
    /// <param name="cancellationToken">Cancels the reconcile.</param>
    /// <returns>A task that completes when the schedule matches the current data and settings.</returns>
    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // The web head cannot schedule while closed, so it reports unsupported and this is a clean no-op.
        if (!await scheduler.IsSupportedAsync(cancellationToken))
        {
            return;
        }

        var enabled = await preferences.GetEnabledAsync(cancellationToken);
        var now = time.GetUtcNow();
        IReadOnlyList<ScheduledNotification> wanted = [];
        if (enabled.Count > 0)
        {
            var inputs = await BuildInputsAsync(enabled, cancellationToken);
            var settings = await preferences.GetSettingsAsync(cancellationToken);

            // The device's time-zone rules for each target date, so a winter reminder planned in summer keeps its
            // local time (Q9).
            var planned = ReminderPlanner.Plan(inputs, enabled, now, settings, time.LocalTimeZone);
            wanted = [.. planned.Select(r => new ScheduledNotification(
                ReminderScheduleDiff.IdFor(r), Title(r), Body(r), r.When))];
        }

        // Only future reminders that changed or went away are cancelled; one already on screen stays (R12).
        var pending = await scheduler.GetPendingAsync(cancellationToken);
        var changes = ReminderScheduleDiff.Compute(pending, wanted, now);
        await scheduler.CancelAsync(changes.Cancel, cancellationToken);
        foreach (var notification in changes.Schedule)
        {
            await scheduler.ScheduleAsync(notification, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<OccasionReminderInput>> BuildInputsAsync(
        IReadOnlySet<ReminderKind> enabled, CancellationToken cancellationToken)
    {
        var myId = (await auth.GetAuthenticationStateAsync()).User.UserId();
        var summaries = await sender.Send(new GetGroupsQuery(), cancellationToken);
        var inputs = new List<OccasionReminderInput>();

        foreach (var summary in summaries)
        {
            var group = await sender.Send(new GetGroupQuery(summary.Id), cancellationToken);
            if (group is null)
            {
                continue;
            }

            // Who "I" am in a group is this device's role there (ADR-0086): the member its own view is about.
            var myMemberId = myId == Guid.Empty
                ? (Guid?)null
                : (await records.LoadAsync(cancellationToken)).Records.FirstOrDefault(r => r.Id == group.Id)?.View?.MemberId;

            foreach (var occasion in group.Events)
            {
                if (occasion.Date is not { } date)
                {
                    continue;
                }

                var myWishlistEmpty = false;
                if (enabled.Contains(ReminderKind.MyWishlistEmpty) && myMemberId is { } memberId)
                {
                    var view = await sender.Send(new GetEventQuery(summary.Id, occasion.Id), cancellationToken);
                    var wishlist = view?.Wishlists.FirstOrDefault(w => w.MemberId == memberId);
                    myWishlistEmpty = wishlist is null || wishlist.Items.Count == 0;
                }

                inputs.Add(new OccasionReminderInput(
                    summary.Id,
                    occasion.Id,
                    string.IsNullOrWhiteSpace(occasion.Name) ? localizer["DefaultEventName"] : occasion.Name,
                    DateOnly.FromDateTime(date.Date),
                    occasion.HasCurrentDraw,
                    myWishlistEmpty));
            }
        }

        return inputs;
    }

    private string Title(PlannedReminder reminder) => reminder.Kind switch
    {
        ReminderKind.DrawNotDone => localizer["ReminderDrawTitle"],
        ReminderKind.EventTomorrow when reminder.DaysBefore > 1 => localizer["ReminderSoonTitle", reminder.EventName, reminder.DaysBefore],
        ReminderKind.EventTomorrow => localizer["ReminderTomorrowTitle", reminder.EventName],
        ReminderKind.MyWishlistEmpty => localizer["ReminderWishlistTitle"],
        _ => reminder.EventName,
    };

    private string Body(PlannedReminder reminder) => reminder.Kind switch
    {
        ReminderKind.DrawNotDone => localizer["ReminderDrawBody", reminder.EventName],
        ReminderKind.EventTomorrow when reminder.DaysBefore > 1 => localizer["ReminderSoonBody", reminder.DaysBefore],
        ReminderKind.EventTomorrow => localizer["ReminderTomorrowBody"],
        ReminderKind.MyWishlistEmpty => localizer["ReminderWishlistBody", reminder.EventName],
        _ => string.Empty,
    };

    /// <inheritdoc />
    public void Dispose()
    {
        autoSync.LocalChanged -= RequestReconcile;
        autoSync.SyncCompleted -= RequestReconcile;
        foreground.Changed -= OnForegroundChanged;
        _debounce?.Cancel();
        _debounce?.Dispose();
    }
}
