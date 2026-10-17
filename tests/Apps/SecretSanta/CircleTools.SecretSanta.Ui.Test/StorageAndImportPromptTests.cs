using Bunit;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Shared;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// WP-2 (ADR-0081, ADR-0044 and ADR-0071 amendments of 2026-10-16): the storage choices a signed-in or
/// signed-out person sees, and the sign-in import prompt that names the groups and has no discard.
/// </summary>
public sealed class StorageAndImportPromptTests : IDisposable
{
    private readonly Bunit.TestContext _ctx = new();

    public StorageAndImportPromptTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
    }

    [Fact]
    public void SignedIn_TheChoicesAreDevice_Account_AccountShared()
    {
        var cut = _ctx.RenderComponent<StorageModePicker>(p => p.Add(x => x.AccountEnabled, true));

        var titles = cut.FindAll(".ct-mode-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["ModeLocalTitle", "ModeAccountTitle", "ModeAccountSharedTitle"], titles);
    }

    [Fact]
    public void SignedOut_TheChoicesAreDevice_Link_AccountDisabled()
    {
        var cut = _ctx.RenderComponent<StorageModePicker>(p => p.Add(x => x.AccountEnabled, false));

        var titles = cut.FindAll(".ct-mode-title").Select(e => e.TextContent).ToList();
        Assert.Equal(["ModeLocalTitle", "ModeSharedTitle", "ModeAccountTitle"], titles);
        Assert.True(cut.Find("#mode-account").HasAttribute("disabled"));
    }

    [Fact]
    public void TheCreateField_ExplainsEveryOption_TheFirstTime()
    {
        var cut = _ctx.RenderComponent<StorageModeField>(p => p
            .Add(x => x.AccountEnabled, false)
            .Add(x => x.Expanded, true));

        Assert.NotNull(cut.Find("#storage-expanded"));
        Assert.Contains("ModeAccountSignInNeeded", cut.Markup);
        Assert.Equal(3, cut.FindAll(".ct-mode-hint").Count);
    }

    [Fact]
    public async Task TheImportPrompt_NamesTheGroups_AndKeepsTheUntickedOnesLocal()
    {
        var provider = _ctx.RenderComponent<MudDialogProvider>();
        var dialogs = _ctx.Services.GetRequiredService<IDialogService>();
        var family = new ClaimableGroup(Guid.NewGuid(), "Family", 4, HasCurrentDraw: true, IsAccountFree: true, StorageMode.OnThisDevice);
        var test = new ClaimableGroup(Guid.NewGuid(), "Test", 0, HasCurrentDraw: false, IsAccountFree: true, StorageMode.OnThisDevice);

        Task<ReconcileAnswer?> asked = null!;
        await provider.InvokeAsync(() => { asked = dialogs.ShowReconcileSheetAsync([family, test]); });

        provider.WaitForAssertion(() => Assert.Contains("Family", provider.Markup));
        Assert.Contains("Test", provider.Markup);
        Assert.Contains("ReconcileSummaryDrawn", provider.Markup);
        Assert.Empty(provider.FindAll("#reconcile-discard")); // no discard in this prompt

        // Untick "Test", then import.
        provider.FindAll("#reconcile-groups input[type=checkbox]")[1].Change(false);
        provider.Find("#reconcile-import").Click();

        var answer = await asked;
        Assert.NotNull(answer);
        Assert.Equal([family.Id], answer.Import);
        Assert.Equal([test.Id], answer.KeepLocal);
    }

    [Fact]
    public async Task Later_DecidesNothing()
    {
        var provider = _ctx.RenderComponent<MudDialogProvider>();
        var dialogs = _ctx.Services.GetRequiredService<IDialogService>();
        var family = new ClaimableGroup(Guid.NewGuid(), "Family", 4, false, true, StorageMode.OnThisDevice);

        Task<ReconcileAnswer?> asked = null!;
        await provider.InvokeAsync(() => { asked = dialogs.ShowReconcileSheetAsync([family]); });
        provider.WaitForAssertion(() => provider.Find("#reconcile-later").Click());

        Assert.Null(await asked);
    }

    public void Dispose() => _ctx.Dispose();
}
