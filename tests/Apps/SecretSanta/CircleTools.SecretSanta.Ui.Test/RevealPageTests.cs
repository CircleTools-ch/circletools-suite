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
/// Component tests for the reveal page (FEAT-001 / ADR-0014): it decodes the fragment client-side
/// and keeps the giftee behind a spoiler gate until the reveal button is pressed.
/// </summary>
public sealed class RevealPageTests : IAsyncDisposable
{
    private const string RecipientName = "Recipient-Zoe";

    // Where a short link keeps the slice it fetched (ADR-0082).
    private readonly TestSupport.FakeKeyValueStore _store = new();
    private readonly Bunit.BunitContext _ctx = new();
    private readonly FakeHapticFeedback _haptics = new();
    private readonly FakeOnlineGroupApi _revealApi = new();

    public RevealPageTests()
    {
        // MudBlazor (ADR-0040): the page is a MudBlazor component now; loose JSInterop lets its JS
        // calls (ripples/element refs) no-op instead of failing the render.
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton(new RevealLinkCodec());
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IHapticFeedback>(_haptics);
        _ctx.Services.AddSingleton<CircleTools.Ui.Abstractions.IKeyValueStore>(_store);
        // The reveal page offers "Add to calendar" (MNF-6) when the occasion has a date.
        _ctx.Services.AddSingleton<CircleTools.Ui.Abstractions.ICalendarService>(new TestSupport.FakeCalendarService());
        // Live reveal (ADR-0068): the page resolves the shared-group port to try a live slice for a
        // backend-backed (rt=) link. These local-only links carry no rt=, so it is never called.
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Online.IOnlineGroupApi>(_revealApi);
    }

    private void NavigateToRevealLink(string fragment)
    {
        var navigation = _ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("r" + fragment); // fragment includes its leading '#'
    }

    private static string BuildFragment(RevealPayload payload, string forLabel = "Giver-Al")
    {
        var builder = new RevealLinkBuilder(new RevealLinkOptions("https://secretsanta.example"), new RevealLinkCodec());
        var url = builder.Build(forLabel, Guid.NewGuid(), payload).Url;
        return url[url.IndexOf('#')..];
    }

    [Fact]
    public void RevealPage_HidesRecipientUntilRevealButtonClicked()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 25m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        // Before revealing, the giftee must not be present anywhere in the rendered markup.
        Assert.DoesNotContain(RecipientName, cut.Markup);

        cut.Find("button").Click();

        // After clicking "Reveal", the giftee is shown.
        Assert.Contains(RecipientName, cut.Markup);
    }

    [Fact]
    public void RevealPage_BackendBackedLink_ShowsLiveSlice_OverTheSnapshot()
    {
        // ADR-0068/FEAT-020: an rt= link fetches the live slice, so the giftee/wishlist shown are the
        // current ones — not the (older) snapshot baked into d=.
        var snapshot = new RevealPayload(RevealPayload.CurrentVersion, "Snapshot-Zoe", ["Old scarf"], null, null, null, null, null);
        var live = new RevealPayload(RevealPayload.CurrentVersion, "Live-Max", ["New headphones"], null, null, null, null, null);
        _revealApi.SeedRevealSlice("rt-token", System.Text.Json.JsonSerializer.Serialize(live, RevealJsonContext.Default.RevealPayload));
        NavigateToRevealLink(BuildFragment(snapshot) + "&rt=rt-token");

        var cut = _ctx.Render<Reveal>();
        // The freshness indicator says the details are current (PassThrough localizer echoes the key).
        Assert.Contains("RevealLiveHint", cut.Markup);

        cut.Find("button").Click();

        Assert.Contains("Live-Max", cut.Markup);
        Assert.Contains("New headphones", cut.Markup);
        Assert.DoesNotContain("Snapshot-Zoe", cut.Markup);
    }

    [Fact]
    public void RevealPage_ALinkInAWish_ShowsAsATappableLink()
    {
        // ADR-0083 (I8): a link pasted anywhere in a wish is shown as "Open link (site)"; only http(s) is a link.
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Drone https://www.shop.example/drone", "Socks"], 50m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();
        cut.Find("button").Click();

        var link = cut.Find("a.ct-wishlist-link");
        Assert.Equal("https://www.shop.example/drone", link.GetAttribute("href"));
        Assert.Contains("noopener", link.GetAttribute("rel"));
        Assert.DoesNotContain("https://www.shop.example/drone</", cut.Markup); // the address is not shown as text
        Assert.Contains("Socks", cut.Markup);
    }

    [Fact]
    public void RevealPage_ShortLink_FetchesTheSlice_AndKeepsItForOfflineReopening()
    {
        // ADR-0082: a short link carries only for= and rt=. Online it shows the live slice; once fetched, the
        // slice is kept on the device so the same link reopens offline.
        var live = new RevealPayload(RevealPayload.CurrentVersion, "Live-Max", ["New headphones"], null, null, null, null, null);
        _revealApi.SeedRevealSlice("short-token", System.Text.Json.JsonSerializer.Serialize(live, RevealJsonContext.Default.RevealPayload));
        NavigateToRevealLink("#for=Anna&rt=short-token");

        var cut = _ctx.Render<Reveal>();
        cut.Find("button").Click();
        Assert.Contains("Live-Max", cut.Markup);

        _revealApi.RevealSlicesUnreachable = true;
        var again = _ctx.Render<Reveal>();
        again.Find("button").Click();
        Assert.Contains("Live-Max", again.Markup);
    }

    [Fact]
    public void RevealPage_ShortLink_TheServerNoLongerKnows_SaysTheLinkIsGone()
    {
        NavigateToRevealLink("#for=Anna&rt=expired-token");

        var cut = _ctx.Render<Reveal>();

        Assert.Contains("RevealLinkGone", cut.Markup);
        Assert.DoesNotContain("RevealNeedsConnection", cut.Markup);
    }

    [Fact]
    public void RevealPage_ShortLink_WhoseGroupWasDeleted_SaysGone_AndDropsTheKeptCopy()
    {
        // ST-2 (2026-10-28): a link opened once kept its slice; after the group was deleted the server answers
        // "not found", and the page showed the kept pairing as "maybe offline" instead of saying the link is gone.
        var live = new RevealPayload(RevealPayload.CurrentVersion, "Live-Max", ["New headphones"], null, null, null, null, null);
        _revealApi.SeedRevealSlice("deleted-token", System.Text.Json.JsonSerializer.Serialize(live, RevealJsonContext.Default.RevealPayload));
        NavigateToRevealLink("#for=Anna&rt=deleted-token");
        _ctx.Render<Reveal>();

        _revealApi.RemoveRevealSlice("deleted-token");
        var gone = _ctx.Render<Reveal>();

        Assert.Contains("RevealLinkGone", gone.Markup);
        Assert.DoesNotContain("Live-Max", gone.Markup);

        // The kept copy went with it: offline later, the page has nothing left to show.
        _revealApi.RevealSlicesUnreachable = true;
        Assert.Contains("RevealNeedsConnection", _ctx.Render<Reveal>().Markup);
    }

    [Fact]
    public void RevealPage_ShortLink_FirstOpenedOffline_SaysItNeedsAConnection()
    {
        _revealApi.RevealSlicesUnreachable = true;
        NavigateToRevealLink("#for=Anna&rt=never-fetched");

        var cut = _ctx.Render<Reveal>();

        Assert.Contains("RevealNeedsConnection", cut.Markup);
        Assert.DoesNotContain("RevealMalformed", cut.Markup);
    }

    [Fact]
    public void RevealPage_BackendBackedLink_FallsBackToSnapshot_WhenOffline()
    {
        // Offline (or a revoked token): the reveal still works from the embedded snapshot, and the
        // indicator warns it is a saved copy (NFR-001).
        _revealApi.RevealSlicesUnreachable = true;
        var snapshot = new RevealPayload(RevealPayload.CurrentVersion, "Snapshot-Zoe", ["Old scarf"], null, null, null, null, null);
        NavigateToRevealLink(BuildFragment(snapshot) + "&rt=rt-token");

        var cut = _ctx.Render<Reveal>();
        Assert.Contains("RevealSnapshotHint", cut.Markup);

        cut.Find("button").Click();
        Assert.Contains("Snapshot-Zoe", cut.Markup);
    }

    [Fact]
    public void RevealPage_NamesTheGroupAndEvent_WhenTheLinkCarriesThem()
    {
        // UX review U4: someone in two exchanges must be able to tell which one a link belongs to.
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, [], null, null, null, null, null,
            GroupName: "Familie Muster", EventName: "Weihnachten 2026");
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        Assert.Equal("Familie Muster · Weihnachten 2026", cut.Find("#reveal-context").TextContent.Trim());
        Assert.DoesNotContain(RecipientName, cut.Markup); // the context line never spoils the giftee
    }

    [Fact]
    public void RevealPage_OlderLinkWithoutNames_ShowsNoContextLine()
    {
        var payload = new RevealPayload(RevealPayload.CurrentVersion, RecipientName, [], null, null, null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        Assert.Empty(cut.FindAll("#reveal-context"));
    }

    [Fact]
    public void RevealPage_LocalOnlyLink_ShowsNoFreshnessIndicator()
    {
        // A local-only link carries no rt=, so there is no live source and no indicator either way.
        var payload = new RevealPayload(RevealPayload.CurrentVersion, RecipientName, ["Book"], null, null, null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        Assert.DoesNotContain("RevealLiveHint", cut.Markup);
        Assert.DoesNotContain("RevealSnapshotHint", cut.Markup);
    }

    [Fact]
    public void RevealPage_TapsHapticWhenTheGifteeIsRevealed()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 25m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();
        Assert.Equal(0, _haptics.Taps); // no buzz merely on render

        cut.Find("button").Click();

        // Revealing the giftee produces exactly one confirmation buzz (FEAT-008 W3).
        Assert.Equal(1, _haptics.Taps);
    }

    [Fact]
    public void RevealPage_ShowsBudgetRange_WhenAMinimumIsSet()
    {
        // FR-036: a budget with both bounds reads as a range (min–max), not a single figure.
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 30m, "CHF", null, null, null, BudgetMin: 10m);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        // The budget is shown before the reveal (it is not spoiler-sensitive).
        Assert.Contains(BudgetDisplay.Format(10m, 30m, "CHF"), cut.Markup); // the one budget format (Q28)
    }

    [Fact]
    public void RevealPage_ShowsBareMaximum_WhenNoMinimumIsSet()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 30m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();

        Assert.Contains(BudgetDisplay.Format(null, 30m, "CHF"), cut.Markup); // "up to CHF 30"
        Assert.DoesNotContain("–", cut.Markup); // no range dash for a max-only budget
    }

    [Fact]
    public void RevealPage_ShowsRecipientGiftNote_OnlyAfterReveal()
    {
        // FR-037: the recipient's gift note is shown with the revealed giftee (it names the giftee's needs),
        // so it must stay behind the spoiler gate like the wishlist.
        const string note = "No nuts, please — severe allergy";
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 30m, "CHF", null, null, null, GiftNote: note);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.Render<Reveal>();
        Assert.DoesNotContain(note, cut.Markup); // hidden before reveal

        cut.Find("button").Click();

        Assert.Contains(note, cut.Markup); // shown after reveal
    }

    [Fact]
    public void RevealPage_MalformedFragment_ShowsErrorAndNoRevealButton()
    {
        NavigateToRevealLink("#for=nobody"); // missing the required payload token

        var cut = _ctx.Render<Reveal>();

        Assert.Contains("RevealMalformed", cut.Markup);
        Assert.Empty(cut.FindAll("button"));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
