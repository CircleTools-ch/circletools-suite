using System.Globalization;
using Bunit;
using CircleTools.SecretSanta.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// PR-14: a typing mistake in the date field never costs the date. The owner keeps the last good date and is told
/// not to save while the field shows a text that is no date; only an emptied field removes the date.
/// </summary>
public sealed class TypedDatePickerTests : IAsyncDisposable
{
    private static readonly DateTime ChristmasEve = new(2026, 12, 24);

    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
    private readonly Bunit.BunitContext _ctx = new();
    private readonly List<DateTime?> _published = [];

    public TypedDatePickerTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-CH");
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<TypedDatePicker> Render(DateTime? date) =>
        _ctx.Render<TypedDatePicker>(parameters => parameters
            .Add(picker => picker.Date, date)
            .Add(picker => picker.DateChanged, EventCallback.Factory.Create<DateTime?>(this, _published.Add)));

    private static string Shown(IRenderedComponent<TypedDatePicker> cut) => cut.Find("input").GetAttribute("value") ?? string.Empty;

    // MudBlazor handles a changed text asynchronously, so under load the outcome can arrive after Change returns.
    private static void TypeUnreadable(IRenderedComponent<TypedDatePicker> cut)
    {
        cut.Find("input").Change("hello");
        cut.WaitForState(() => cut.Instance.HasUnreadableText);
    }

    private void AssertPublished(IRenderedComponent<TypedDatePicker> cut, DateTime? date) =>
        cut.WaitForAssertion(() =>
        {
            Assert.Equal([date], _published);
            Assert.False(cut.Instance.HasUnreadableText);
        });

    [Fact]
    public void ATypedDateIsPublished()
    {
        var cut = Render(null);

        cut.Find("input").Change("24.12.2026");

        AssertPublished(cut, ChristmasEve);
    }

    [Fact]
    public void AnUnreadableTextIsNotPublished_AndTheOwnerIsWarned()
    {
        var cut = Render(ChristmasEve);

        TypeUnreadable(cut);

        Assert.Empty(_published);
    }

    [Fact]
    public void AnUnreadableTextAndItsWarningSurviveTheOwnerRendering()
    {
        var cut = Render(ChristmasEve);
        TypeUnreadable(cut);

        cut.Render(parameters => parameters.Add(picker => picker.Date, ChristmasEve));

        Assert.Equal("hello", Shown(cut));
        Assert.True(cut.Instance.HasUnreadableText);
        Assert.Empty(_published);
    }

    [Fact]
    public void AnEmptiedFieldRemovesTheDate()
    {
        var cut = Render(ChristmasEve);

        cut.Find("input").Change(string.Empty);

        AssertPublished(cut, null);
    }

    [Fact]
    public void EmptyingAnUnreadableTextRemovesTheDate()
    {
        var cut = Render(ChristmasEve);
        TypeUnreadable(cut);

        cut.Find("input").Change(string.Empty);

        AssertPublished(cut, null);
    }

    [Fact]
    public void CorrectingAnUnreadableTextPublishesTheDate()
    {
        var cut = Render(new DateTime(2026, 1, 1));
        TypeUnreadable(cut);

        cut.Find("input").Change("24-12-2026");

        AssertPublished(cut, ChristmasEve);
    }

    [Fact]
    public void CorrectingBackToTheSameDateLiftsTheWarning_WithoutPublishing()
    {
        var cut = Render(ChristmasEve);
        TypeUnreadable(cut);

        cut.Find("input").Change("24.12.2026");

        cut.WaitForState(() => !cut.Instance.HasUnreadableText);
        Assert.Empty(_published);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        CultureInfo.CurrentCulture = _previous;
        return _ctx.DisposeAsync();
    }
}
