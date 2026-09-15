using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// Test double for <see cref="ICalendarService"/> (MNF-6): records the occasions handed to it, and reports
/// a configurable "supported" so the "Add to calendar" gate can be exercised.
/// </summary>
public sealed class FakeCalendarService : ICalendarService
{
    /// <summary>Whether the platform reports it can add to a calendar.</summary>
    public bool Supported { get; set; } = true;

    /// <summary>The occasions passed to <see cref="AddToCalendarAsync"/>, in order.</summary>
    public List<CalendarEvent> Added { get; } = [];

    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Supported);

    /// <inheritdoc />
    public ValueTask AddToCalendarAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        Added.Add(calendarEvent);
        return ValueTask.CompletedTask;
    }
}
