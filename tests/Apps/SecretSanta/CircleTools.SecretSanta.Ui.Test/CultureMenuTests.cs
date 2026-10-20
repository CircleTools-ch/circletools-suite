using CircleTools.Localization;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Shared;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The compact language switch on pages opened from a link (2026-10-17): it shows the current language and
/// switches in place, so a reveal or join link no longer needs a trip to Settings that loses the link.
/// </summary>
public sealed class CultureMenuTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();
    private readonly RecordingCultureProvider _culture = new("de-CH");

    public CultureMenuTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<ICultureProvider>(_culture);
        _ctx.Services.AddSingleton<IStringLocalizer<SharedStrings>>(new PassThroughStringLocalizer<SharedStrings>());
    }

    [Fact]
    public async Task ShowsTheCurrentLanguage_AndSwitchesToTheOneChosen()
    {
        var popover = _ctx.Render<MudBlazor.MudPopoverProvider>();
        var cut = _ctx.Render<CultureMenu>();
        Assert.Contains("DE", cut.Find("#culture-menu").TextContent);

        await cut.InvokeAsync(() => cut.Find("#culture-menu").Click());
        popover.WaitForAssertion(() => Assert.Equal(AppCultures.Supported.Count, popover.FindAll(".mud-menu-item").Count));
        var french = AppCultures.DisplayName("fr-CH");
        await popover.InvokeAsync(() => popover.FindAll(".mud-menu-item").First(i => i.TextContent.Contains(french, StringComparison.Ordinal)).Click());

        Assert.Equal("fr-CH", _culture.LastSet);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
