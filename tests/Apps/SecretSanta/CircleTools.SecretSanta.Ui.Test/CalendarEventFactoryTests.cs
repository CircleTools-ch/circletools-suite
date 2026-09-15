using System.Globalization;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="CalendarEventFactory"/> (MNF-6, FR-044): an occasion with no date yields no
/// entry; a dated one carries the app event route as its reopen link, a stable UID, and notes with the
/// budget range appended.
/// </summary>
public sealed class CalendarEventFactoryTests
{
    private static readonly Guid Group = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Event = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string BaseUrl = "https://secretsanta.circletools.ch";

    public CalendarEventFactoryTests()
    {
        // The budget range uses the current culture; pin it so "10–30" is not localized to "10,30".
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void ReturnsNull_WhenTheOccasionHasNoDate()
    {
        var result = CalendarEventFactory.ForOccasion(
            Group, Event, "Christmas", date: null, location: null, notes: null, budget: null,
            budgetLabel: "Budget", baseUrl: BaseUrl);

        Assert.Null(result);
    }

    [Fact]
    public void BuildsTheEntry_WithTheAppEventRoute_AndAStableUid()
    {
        var date = new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CalendarEventFactory.ForOccasion(
            Group, Event, "Christmas 2026", date, "Bern", notes: null, budget: null,
            budgetLabel: "Budget", baseUrl: BaseUrl);

        Assert.NotNull(result);
        Assert.Equal("Christmas 2026", result!.Title);
        Assert.Equal(new DateOnly(2026, 12, 24), result.Date);
        Assert.Equal("Bern", result.Location);
        Assert.Equal($"{BaseUrl}/groups/{Group}/events/{Event}", result.Url);
        Assert.Equal($"secretsanta-{Group}-{Event}@circletools.ch", result.Uid);
    }

    [Fact]
    public void OmitsTheLink_WhenNoBaseUrlIsConfigured()
    {
        var date = new DateTimeOffset(2026, 12, 24, 0, 0, 0, TimeSpan.Zero);

        var result = CalendarEventFactory.ForOccasion(
            Group, Event, "X", date, location: "   ", notes: null, budget: null, budgetLabel: "Budget", baseUrl: null);

        Assert.NotNull(result);
        Assert.Null(result!.Url);
        Assert.Null(result.Location); // whitespace-only location is dropped
    }

    [Fact]
    public void ComposesNotes_WithTheBudgetRange()
    {
        var notes = CalendarEventFactory.ComposeNotes(
            "Bring something homemade", new BudgetDto(30m, "CHF", 10m), "Budget");

        Assert.Equal("Bring something homemade\nBudget: 10–30 CHF", notes);
    }

    [Fact]
    public void ComposesNotes_BudgetOnly_AndNotesOnly_AndNeither()
    {
        Assert.Equal("Budget: 30 CHF", CalendarEventFactory.ComposeNotes(null, new BudgetDto(30m, "CHF"), "Budget"));
        Assert.Equal("Just notes", CalendarEventFactory.ComposeNotes("Just notes", null, "Budget"));
        Assert.Null(CalendarEventFactory.ComposeNotes(null, null, "Budget"));
        Assert.Null(CalendarEventFactory.ComposeNotes("   ", null, "Budget"));
    }
}
