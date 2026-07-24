using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Reveal;
using AppSuite.SecretSanta.Ui.Pages;
using AppSuite.SecretSanta.Ui.Test.TestSupport;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace AppSuite.SecretSanta.Ui.Test;

/// <summary>
/// Component tests for the reveal page (FEAT-001 / ADR-0014): it decodes the fragment client-side
/// and keeps the giftee behind a spoiler gate until the reveal button is pressed.
/// </summary>
public sealed class RevealPageTests : IDisposable
{
    private const string RecipientName = "Recipient-Zoe";

    private readonly Bunit.TestContext _ctx = new();

    public RevealPageTests()
    {
        _ctx.Services.AddSingleton(new RevealLinkCodec());
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
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
