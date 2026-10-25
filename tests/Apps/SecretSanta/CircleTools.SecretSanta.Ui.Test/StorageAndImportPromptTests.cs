using Bunit;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Shared;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The place choices (ADR-0086): on this device or online, both always available. What "online" means follows the
/// sign-in — in the account while signed in, organized by a management link while signed out.
/// </summary>
public sealed class StorageAndImportPromptTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();

    public StorageAndImportPromptTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
    }

    [Fact]
    public void SignedIn_TheChoicesAreDevice_Online_InTheAccount()
    {
        var cut = _ctx.Render<StorageModePicker>(p => p.Add(x => x.AccountEnabled, true));

        Assert.Equal(["ModeLocalTitle", "PlaceOnlineTitle"], cut.FindAll(".ct-mode-title").Select(e => e.TextContent).ToList());
        Assert.Contains("PlaceOnlineHint", cut.Markup);
        Assert.False(cut.Find("#mode-online").HasAttribute("disabled"));
    }

    [Fact]
    public void SignedOut_OnlineIsStillOffered_OrganizedByALink()
    {
        var cut = _ctx.Render<StorageModePicker>(p => p.Add(x => x.AccountEnabled, false));

        Assert.Equal(["ModeLocalTitle", "PlaceOnlineTitle"], cut.FindAll(".ct-mode-title").Select(e => e.TextContent).ToList());
        Assert.Contains("PlaceOnlineSignedOutHint", cut.Markup);
        Assert.False(cut.Find("#mode-online").HasAttribute("disabled"));
    }

    [Fact]
    public void TheCreateField_ExplainsBothOptions_TheFirstTime()
    {
        var cut = _ctx.Render<StorageModeField>(p => p
            .Add(x => x.AccountEnabled, false)
            .Add(x => x.Expanded, true));

        Assert.NotNull(cut.Find("#storage-expanded"));
        Assert.Equal(2, cut.FindAll(".ct-mode-hint").Count);
    }

    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
