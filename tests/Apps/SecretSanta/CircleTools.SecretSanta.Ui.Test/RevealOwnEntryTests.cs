using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Reveal;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The reveal-link holder keeps their own wishlist and gift note on the reveal page (ADR-0084, FR-050, TP-026):
/// shown only when the server offers the entry for the link's reveal token, autosaved, honest when a save
/// fails; every other link keeps the "tell the organizer" hint.
/// </summary>
public sealed class RevealOwnEntryTests : IDisposable
{
    private const string Token = "rt-dario";

    private readonly Bunit.TestContext _ctx = new();
    private readonly NoOpSharedGroupApi _api = new();

    public RevealOwnEntryTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(new RevealLinkCodec());
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IHapticFeedback>(new FakeHapticFeedback());
        _ctx.Services.AddSingleton<CircleTools.Ui.Abstractions.IKeyValueStore>(new FakeKeyValueStore());
        _ctx.Services.AddSingleton<CircleTools.Ui.Abstractions.ICalendarService>(new FakeCalendarService());
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.ISharedGroupApi>(_api);
    }

    [Fact]
    public void ALiveLink_OffersTheHoldersOwnEntry_InsteadOfTheHint()
    {
        _api.SeedRevealEntry(Token, new RevealEntryView("no nuts", ["Lego", "Book"]));

        var cut = OpenAndReveal(live: true);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#reveal-own-entry")));
        Assert.Equal("Lego\nBook", Field(cut, "reveal-own-wishlist"));
        Assert.Equal("no nuts", Field(cut, "reveal-own-gift-note"));
        Assert.DoesNotContain("RevealNextStep", cut.Markup);
    }

    [Fact]
    public void TheOwnEntry_IsShownBeforeTheReveal_WithoutGivingAwayTheRecipient()
    {
        // It is about the holder, not whom they drew — no spoiler, so it is not behind the reveal button.
        _api.SeedRevealEntry(Token, new RevealEntryView(null, ["Lego"]));

        var cut = Open(live: true);

        cut.WaitForAssertion(() => Assert.Equal("Lego", Field(cut, "reveal-own-wishlist")));
        Assert.Contains("RevealButton", cut.Markup);
        Assert.Empty(cut.FindAll("#reveal-result"));
        Assert.DoesNotContain("Anna", cut.Markup);
    }

    [Fact]
    public void LeavingTheFieldAfterTyping_SavesTheWishlistAndNote_ByTheRevealToken()
    {
        _api.SeedRevealEntry(Token, new RevealEntryView(null, []));
        var cut = OpenAndReveal(live: true);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#reveal-own-wishlist")));

        Input(cut, "reveal-own-wishlist", "Lego\n  \nDuplo");
        Input(cut, "reveal-own-gift-note", "no nuts");
        Blur(cut, "reveal-own-gift-note");

        cut.WaitForAssertion(() =>
        {
            var (token, entry) = Assert.Single(_api.RevealEntryWrites);
            Assert.Equal(Token, token);
            Assert.Equal(["Lego", "Duplo"], entry.Items);
            Assert.Equal("no nuts", entry.GiftNote);
            Assert.Contains("SavedCheck", cut.Markup);
        });
    }

    [Fact]
    public void AFailedSave_KeepsTheText_SaysSo_AndTheNextChangeRetries()
    {
        _api.SeedRevealEntry(Token, new RevealEntryView(null, []));
        var cut = OpenAndReveal(live: true);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#reveal-own-wishlist")));

        _api.RevealEntryWritesFail = true;
        Input(cut, "reveal-own-wishlist", "Lego");
        Blur(cut, "reveal-own-wishlist");
        cut.WaitForAssertion(() => Assert.Contains("WishlistSaveFailed", cut.Markup));
        Assert.Equal("Lego", Field(cut, "reveal-own-wishlist"));

        _api.RevealEntryWritesFail = false;
        Input(cut, "reveal-own-wishlist", "Lego Duplo");
        Blur(cut, "reveal-own-wishlist");
        cut.WaitForAssertion(() => Assert.Equal(["Lego Duplo"], Assert.Single(_api.RevealEntryWrites).Entry.Items));
    }

    [Fact]
    public void LeavingAChangedField_OffersUndo_WhichSavesTheEarlierWishlistAndNote()
    {
        _api.SeedRevealEntry(Token, new RevealEntryView("no nuts", ["Lego"]));
        var snackbars = _ctx.RenderComponent<MudBlazor.MudSnackbarProvider>();
        var cut = OpenAndReveal(live: true);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#reveal-own-wishlist")));

        Input(cut, "reveal-own-wishlist", "");
        Blur(cut, "reveal-own-wishlist");
        cut.WaitForAssertion(() => Assert.Empty(Assert.Single(_api.RevealEntryWrites).Entry.Items));

        ClickUndo(snackbars);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, _api.RevealEntryWrites.Count);
            Assert.Equal(["Lego"], _api.RevealEntryWrites[1].Entry.Items);
            Assert.Equal("no nuts", _api.RevealEntryWrites[1].Entry.GiftNote);
        });
        cut.WaitForAssertion(() => Assert.Equal("Lego", Field(cut, "reveal-own-wishlist")));
    }

    [Fact]
    public void ALiveLinkWithoutAnEntry_ShowsNoEditor_AndKeepsTheHint()
    {
        var cut = OpenAndReveal(live: true); // the server answers 404: older slice, or a linked member

        cut.WaitForAssertion(() => Assert.Contains("RevealNextStep", cut.Markup));
        Assert.Empty(cut.FindAll("#reveal-own-entry"));
    }

    [Fact]
    public void ALocalOnlyLink_ShowsNoEditor_AndKeepsTheHint()
    {
        var cut = OpenAndReveal(live: false);

        Assert.Contains("RevealNextStep", cut.Markup);
        Assert.Empty(cut.FindAll("#reveal-own-entry"));
    }

    private IRenderedComponent<Reveal> OpenAndReveal(bool live)
    {
        var cut = Open(live);
        cut.Find("button").Click();
        return cut;
    }

    private IRenderedComponent<Reveal> Open(bool live)
    {
        var payload = new RevealPayload(RevealPayload.CurrentVersion, "Anna", ["Scarf"], null, null, null, null, null);
        var builder = new RevealLinkBuilder(new RevealLinkOptions("https://secretsanta.example"), new RevealLinkCodec());
        var url = builder.Build("Dario", Guid.NewGuid(), payload).Url;
        var fragment = url[url.IndexOf('#')..] + (live ? $"&rt={Token}" : string.Empty);
        _ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("r" + fragment);

        return _ctx.RenderComponent<Reveal>();
    }

    // Input/blur inside the renderer's dispatcher: each keystroke re-renders, so an element found outside it
    // can carry a retired handler id by the time the event is raised.
    private static void Input(IRenderedComponent<Reveal> cut, string id, string text) =>
        cut.InvokeAsync(() => FieldElement(cut, id).Input(text)).GetAwaiter().GetResult();

    private static void Blur(IRenderedComponent<Reveal> cut, string id) =>
        cut.InvokeAsync(() => FieldElement(cut, id).Blur()).GetAwaiter().GetResult();

    // The undo offer is a snackbar action (UndoLastEdit); the provider renders it outside the page.
    private static void ClickUndo(IRenderedComponent<MudBlazor.MudSnackbarProvider> snackbars)
    {
        snackbars.WaitForAssertion(() => Assert.Contains(snackbars.FindAll("button"), b => b.TextContent.Trim() == "Undo"));
        snackbars.InvokeAsync(() => snackbars.FindAll("button").First(b => b.TextContent.Trim() == "Undo").Click()).GetAwaiter().GetResult();
    }

    private static string? Field(IRenderedComponent<Reveal> cut, string id) =>
        FieldElement(cut, id).GetAttribute("value") ?? FieldElement(cut, id).TextContent;

    private static AngleSharp.Dom.IElement FieldElement(IRenderedComponent<Reveal> cut, string id) =>
        cut.Find($"#{id}");

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}
