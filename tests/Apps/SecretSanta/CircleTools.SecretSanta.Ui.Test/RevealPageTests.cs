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
public sealed class RevealPageTests : IDisposable
{
    private const string RecipientName = "Recipient-Zoe";

    private readonly Bunit.TestContext _ctx = new();
    private readonly FakeHapticFeedback _haptics = new();

    public RevealPageTests()
    {
        // MudBlazor (ADR-0040): the page is a MudBlazor component now; loose JSInterop lets its JS
        // calls (ripples/element refs) no-op instead of failing the render.
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton(new RevealLinkCodec());
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IHapticFeedback>(_haptics);
        // The reveal page offers "Add to calendar" (MNF-6) when the occasion has a date.
        _ctx.Services.AddSingleton<CircleTools.Ui.Abstractions.ICalendarService>(new TestSupport.FakeCalendarService());
    }

    private void NavigateToRevealLink(string fragment)
    {
        var navigation = _ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("r" + fragment); // fragment includes its leading '#'
    }

    private static string BuildFragment(RevealPayload payload, string forLabel = "Giver-Al")
    {
        var builder = new RevealLinkBuilder(new RevealLinkOptions("https://secretsanta.example"), new RevealLinkCodec());
        var url = builder.Build(forLabel, payload).Url;
        return url[url.IndexOf('#')..];
    }

    [Fact]
    public void RevealPage_HidesRecipientUntilRevealButtonClicked()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 25m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.RenderComponent<Reveal>();

        // Before revealing, the giftee must not be present anywhere in the rendered markup.
        Assert.DoesNotContain(RecipientName, cut.Markup);

        cut.Find("button").Click();

        // After clicking "Reveal", the giftee is shown.
        Assert.Contains(RecipientName, cut.Markup);
    }

    [Fact]
    public void RevealPage_TapsHapticWhenTheGifteeIsRevealed()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 25m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.RenderComponent<Reveal>();
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

        var cut = _ctx.RenderComponent<Reveal>();

        // The budget is shown before the reveal (it is not spoiler-sensitive).
        Assert.Contains("10–30 CHF", cut.Markup);
    }

    [Fact]
    public void RevealPage_ShowsBareMaximum_WhenNoMinimumIsSet()
    {
        var payload = new RevealPayload(
            RevealPayload.CurrentVersion, RecipientName, ["Book"], 30m, "CHF", null, null, null);
        NavigateToRevealLink(BuildFragment(payload));

        var cut = _ctx.RenderComponent<Reveal>();

        Assert.Contains("30 CHF", cut.Markup);
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

        var cut = _ctx.RenderComponent<Reveal>();
        Assert.DoesNotContain(note, cut.Markup); // hidden before reveal

        cut.Find("button").Click();

        Assert.Contains(note, cut.Markup); // shown after reveal
    }

    [Fact]
    public void RevealPage_MalformedFragment_ShowsErrorAndNoRevealButton()
    {
        NavigateToRevealLink("#for=nobody"); // missing the required payload token

        var cut = _ctx.RenderComponent<Reveal>();

        Assert.Contains("RevealMalformed", cut.Markup);
        Assert.Empty(cut.FindAll("button"));
    }

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}
