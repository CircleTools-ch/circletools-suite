// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using CircleTools.Circles.Contracts;
using CircleTools.Main.Ui.Pages.Circles;
using CircleTools.Main.Ui.Test.TestSupport;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CircleTools.Main.Ui.Test;

/// <summary>
/// The two destructive confirmations rendered with the <strong>real</strong> DE/EN/FR resources,
/// rather than the pass-through localizer the rest of the component tests use.
/// </summary>
/// <remarks>
/// <para>
/// Every other test in this project asserts on resource <em>keys</em>, which is the right trade for
/// role-dependent rendering and empty states — it keeps them independent of wording. It cannot see the
/// class of defect this milestone's requirements are most exposed to, though: a sentence assembled
/// from a format string and a value, where the placeholder was dropped in translation, or the value
/// lands against its neighbours with no separator. Both of FEAT-003's confirmations are exactly that
/// shape, and both are acceptance criteria (FR-021, FR-022).
/// </para>
/// <para>
/// This is not a substitute for looking at the screen — spacing, wrapping and emphasis need eyes. It
/// is what can be asserted mechanically about the prose a user actually reads.
/// </para>
/// </remarks>
public sealed class CircleConfirmationTextTests
{
    private static readonly string[] _languages = ["de-CH", "en", "fr-CH"];

    /// <summary>
    /// FR-022: the confirmation names the circle, counts who loses access, names every managed member,
    /// and says what deletion does not reach — in every language, as sentences rather than fragments.
    /// </summary>
    [Theory]
    [InlineData("de-CH")]
    [InlineData("en")]
    [InlineData("fr-CH")]
    public async Task DeleteConfirmation_ReadsAsSentences_InEveryLanguage(string language)
    {
        using var scope = new CultureScope(language);
        await using var context = RealTextContext();

        var circleId = Guid.CreateVersion7();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/circles/{circleId}",
            new CircleDetailResponse(
                circleId,
                "Müller",
                "Familie",
                "Owner",
                [
                    Person("Anna", "Owner", isYou: true),
                    Person("Bea"),
                    Person("Chris"),
                    Person("Grosi", isManaged: true),
                    Person("Grosspapa", isManaged: true),
                ]));
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/circles/{circleId}/invitations",
            Array.Empty<PendingInvitationResponse>());

        var text = Normalize(RenderDetail(context, circleId).Find(".circletools-danger").TextContent);

        // The circle, by name, inside its sentence rather than dangling beside it.
        Assert.Contains("Müller", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", text, StringComparison.Ordinal);

        // Three account-backed people lose access; the two managed members are deleted, not "losing
        // access" — they never had any. Getting this count wrong is the likeliest quiet mistake here.
        Assert.Contains("3", text, StringComparison.Ordinal);
        Assert.Contains("Grosi, Grosspapa", text, StringComparison.Ordinal);

        // No value has run into its neighbour: every name and number is separated from the words
        // around it. This is the assertion that would have caught "Anlass:24.12.2026Zürich".
        Assert.Matches(@"(^|\s|[«""'(]|>)Müller($|[\s.,;:!?»""')])", text);
        Assert.Matches(@"(^|\s)3(\s|$)", text);
    }

    /// <summary>
    /// FR-022 at a count of one — the owner alone in their circle, which is the state every circle
    /// starts in and the one the browser walk-through hit first.
    /// </summary>
    /// <remarks>
    /// The plural test above uses three people and so cannot see this: German, English and French all
    /// inflect the noun and the verb, and a single format string produced "1 Personen verlieren",
    /// "1 people will lose" and "1 personnes en perdront" in the running app. Asserting the digit is
    /// absent is what pins it — the singular sentence spells the number as a word in all three.
    /// </remarks>
    [Theory]
    [InlineData("de-CH")]
    [InlineData("en")]
    [InlineData("fr-CH")]
    public async Task DeleteConfirmation_UsesTheSingular_WhenOnlyTheOwnerLosesAccess(string language)
    {
        using var scope = new CultureScope(language);
        await using var context = RealTextContext();

        var circleId = Guid.CreateVersion7();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/circles/{circleId}",
            new CircleDetailResponse(
                circleId,
                "Müller",
                "Familie",
                "Owner",
                [Person("Anna", "Owner", isYou: true), Person("Grosi", isManaged: true)]));
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/circles/{circleId}/invitations",
            Array.Empty<PendingInvitationResponse>());

        var text = Normalize(RenderDetail(context, circleId).Find(".circletools-danger").TextContent);

        // The managed member is deleted, not counted among those losing access — so a "2" here would
        // mean the count had started including people who never had access.
        Assert.DoesNotContain("2", text, StringComparison.Ordinal);

        // No bare count at all: at one, the sentence says it in words.
        Assert.DoesNotMatch(@"(^|\s)1(\s|$)", text);

        // Grosi is still named as deleted — the singular sentence replaces the count, not the rest.
        Assert.Contains("Grosi", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-021: the leave confirmation says what leaving does not take. The words differ per language;
    /// that all three say something about the account and about Secret-Santa is what is checked here,
    /// and the exact prose is a translation review.
    /// </summary>
    [Theory]
    [InlineData("de-CH", "Konto")]
    [InlineData("en", "account")]
    [InlineData("fr-CH", "compte")]
    public async Task LeaveConfirmation_MentionsTheAccountAndSecretSanta_InEveryLanguage(string language, string account)
    {
        using var scope = new CultureScope(language);
        await using var context = RealTextContext();

        var circleId = Guid.CreateVersion7();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/circles/{circleId}",
            new CircleDetailResponse(
                circleId,
                "Müller",
                "Familie",
                "Member",
                [Person("Anna", "Owner"), Person("Bea", isYou: true)]));

        var page = RenderDetail(context, circleId);
        page.FindAll("button")
            .First(b => b.TextContent.Contains(Text(context, "CircleLeave"), StringComparison.Ordinal))
            .Click();

        var text = Normalize(page.Find(".mud-alert").TextContent);

        Assert.Contains(account, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secret-Santa", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", text, StringComparison.Ordinal);
    }

    /// <summary>Every language renders every key — an untranslated key would surface as its own name.</summary>
    [Fact]
    public async Task NoConfirmationRendersARawResourceKey()
    {
        foreach (var language in _languages)
        {
            using var scope = new CultureScope(language);
            await using var context = RealTextContext();

            var circleId = Guid.CreateVersion7();
            context.Api.RespondJson(
                HttpMethod.Get,
                $"api/v1/circles/{circleId}",
                new CircleDetailResponse(
                    circleId,
                    "Müller",
                    "Familie",
                    "Owner",
                    [Person("Anna", "Owner", isYou: true), Person("Grosi", isManaged: true)]));
            context.Api.RespondJson(
                HttpMethod.Get,
                $"api/v1/circles/{circleId}/invitations",
                Array.Empty<PendingInvitationResponse>());

            var text = RenderDetail(context, circleId).Find(".circletools-danger").TextContent;

            Assert.DoesNotContain("CircleDelete", text, StringComparison.Ordinal);
        }
    }

    private static CirclePersonResponse Person(
        string name,
        string role = "Member",
        bool isManaged = false,
        bool isYou = false) =>
        new(Guid.CreateVersion7(), name, role, isManaged, isYou);

    /// <summary>
    /// A context whose localizer reads the shipped <c>.resx</c> files. The closed registration is added
    /// after the base class's open generic, so it wins for <c>IStringLocalizer&lt;AppStrings&gt;</c>
    /// while the shared components keep their pass-through one.
    /// </summary>
    private static AccountTestContext RealTextContext()
    {
        var context = new AccountTestContext();

        context.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        context.Services.AddSingleton<IStringLocalizer<AppStrings>>(services =>
            new StringLocalizer<AppStrings>(services.GetRequiredService<IStringLocalizerFactory>()));

        context.SignIn();

        return context;
    }

    private static string Text(AccountTestContext context, string key) =>
        context.Services.GetRequiredService<IStringLocalizer<AppStrings>>()[key].Value;

    private static IRenderedComponent<CircleDetail> RenderDetail(AccountTestContext context, Guid circleId) =>
        context.Render<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(
                parameters => parameters.AddChildContent<CircleDetail>(
                    child => child.Add(p => p.CircleId, circleId)))
            .FindComponent<CircleDetail>();

    /// <summary>Collapses rendered whitespace so assertions are about words, not about indentation.</summary>
    private static string Normalize(string text) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

    /// <summary>Renders under a given language, and puts the culture back afterwards.</summary>
    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _previousFormatting = CultureInfo.CurrentCulture;

        public CultureScope(string language)
        {
            var culture = new CultureInfo(language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentUICulture = _previous;
            CultureInfo.CurrentCulture = _previousFormatting;
        }
    }
}
