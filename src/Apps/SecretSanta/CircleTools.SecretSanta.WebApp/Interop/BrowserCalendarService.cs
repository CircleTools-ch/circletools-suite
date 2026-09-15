using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.SecretSanta.WebApp.Interop;

/// <summary>
/// Hands the shared iCalendar (<c>.ics</c>) entry to the OS through <c>wwwroot/js/calendar.js</c>
/// (ADR-0028 amendment) — a Blob download the browser routes to the calendar app (MNF-6). Host-local,
/// beside <see cref="BrowserShareService"/>, until a second web host needs it.
/// </summary>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserCalendarService(IJSRuntime js) : ICalendarService, IAsyncDisposable
{
    private const string FileName = "secret-santa.ics";

    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            return await module.InvokeAsync<bool>("isSupported", cancellationToken);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask AddToCalendarAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var ics = IcsBuilder.Build(calendarEvent);
        try
        {
            var module = await LoadAsync(cancellationToken);
            await module.InvokeVoidAsync("addToCalendar", cancellationToken, FileName, ics);
        }
        catch (JSException)
        {
            // Best-effort (ICalendarService): a browser that refuses the download simply adds nothing.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is going away; there is no runtime left to release the module in.
        }
    }

    private async ValueTask<IJSObjectReference> LoadAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/calendar.js");
}
