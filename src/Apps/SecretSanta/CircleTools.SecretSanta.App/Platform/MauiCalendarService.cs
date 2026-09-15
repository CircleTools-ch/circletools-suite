using CircleTools.Ui.Abstractions;
#if ANDROID
using Android.Content;
using Android.Provider;
#else
using Microsoft.Maui.Storage;
#endif

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="ICalendarService"/> (ADR-0028 amendment) — the device-head counterpart of
/// <c>BrowserCalendarService</c>. Hands one occasion to the platform's own calendar UI, pre-filled, with
/// no calendar permission and no silent write (MNF-6).
/// </summary>
/// <remarks>
/// Android has no reliable default handler for a <c>text/calendar</c> file, so it uses an
/// <c>ACTION_INSERT</c> intent on <c>CalendarContract.Events</c>, which opens the calendar's pre-filled
/// new-event editor. Other heads (Windows now, iOS when it is built, ADR-0031) open the shared <c>.ics</c>
/// so the platform Calendar shows its add-event preview.
/// </remarks>
public sealed class MauiCalendarService : ICalendarService
{
    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);

    /// <inheritdoc />
    public ValueTask AddToCalendarAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

#if ANDROID
        // All-day bounds: [Date, Date+1) at UTC midnight, the convention Android's calendar expects when
        // ExtraEventAllDay is set.
        var begin = new DateTimeOffset(calendarEvent.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = begin.AddDays(1);
        var description = ComposeDescription(calendarEvent);

        var intent = new Intent(Intent.ActionInsert)
            .SetData(CalendarContract.Events.ContentUri)
            .PutExtra(CalendarContract.Events.InterfaceConsts.Title, calendarEvent.Title)
            .PutExtra(CalendarContract.ExtraEventAllDay, true)
            .PutExtra(CalendarContract.ExtraEventBeginTime, begin.ToUnixTimeMilliseconds())
            .PutExtra(CalendarContract.ExtraEventEndTime, end.ToUnixTimeMilliseconds());

        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            intent.PutExtra(CalendarContract.Events.InterfaceConsts.EventLocation, calendarEvent.Location);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            intent.PutExtra(CalendarContract.Events.InterfaceConsts.Description, description);
        }

        // Launched from a non-activity context (the DI-resolved service), so a new task is required.
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
        return ValueTask.CompletedTask;
#else
        return AddViaIcsFileAsync(calendarEvent, cancellationToken);
#endif
    }

#if ANDROID
    // Notes first, then the reopen link on its own line — same shape as the .ics DESCRIPTION.
    private static string? ComposeDescription(CalendarEvent calendarEvent)
    {
        var hasNotes = !string.IsNullOrWhiteSpace(calendarEvent.Notes);
        var hasUrl = !string.IsNullOrWhiteSpace(calendarEvent.Url);
        return (hasNotes, hasUrl) switch
        {
            (true, true) => $"{calendarEvent.Notes}\n{calendarEvent.Url}",
            (true, false) => calendarEvent.Notes,
            (false, true) => calendarEvent.Url,
            _ => null,
        };
    }
#else
    // Windows / iOS: write the shared .ics to a temp file and let the OS open it in the calendar app.
    private static async ValueTask AddViaIcsFileAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        var ics = IcsBuilder.Build(calendarEvent);
        var path = Path.Combine(FileSystem.CacheDirectory, "secret-santa.ics");
        await File.WriteAllTextAsync(path, ics, cancellationToken);
        await Launcher.Default.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(path),
        });
    }
#endif
}
