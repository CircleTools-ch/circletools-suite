// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui.Shared;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Component tests for <see cref="AddToCalendarButton"/> (MNF-6, FR-044): the button shows only when a
/// dated occasion is supplied, and clicking it hands that occasion to the <see cref="ICalendarService"/>.
/// </summary>
public sealed class AddToCalendarButtonTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();
    private readonly FakeCalendarService _calendar = new();

    public AddToCalendarButtonTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<ICalendarService>(_calendar);
    }

    [Fact]
    public void RendersNothing_WhenNoOccasionIsSupplied()
    {
        var cut = _ctx.Render<AddToCalendarButton>(p => p.Add(b => b.CalendarEvent, null));

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void RendersTheButton_WhenAnOccasionIsSupplied()
    {
        var cut = Render();

        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "AddToCalendar");
    }

    [Fact]
    public void Clicking_HandsTheOccasionToTheCalendarService()
    {
        var cut = Render();

        cut.Find("#add-to-calendar").Click();

        var added = Assert.Single(_calendar.Added);
        Assert.Equal("Christmas 2026", added.Title);
        Assert.Equal(new DateOnly(2026, 12, 24), added.Date);
    }

    private IRenderedComponent<AddToCalendarButton> Render() =>
        _ctx.Render<AddToCalendarButton>(p => p.Add(
            b => b.CalendarEvent,
            new CalendarEvent("uid", "Christmas 2026", new DateOnly(2026, 12, 24), Location: "Bern")));

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
