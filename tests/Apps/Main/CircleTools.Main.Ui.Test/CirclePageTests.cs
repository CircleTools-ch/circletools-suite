using System.Net;
using CircleTools.Circles.Contracts;
using CircleTools.Main.Ui.Pages.Circles;
using CircleTools.Main.Ui.Test.TestSupport;
using Bunit;

namespace CircleTools.Main.Ui.Test;

/// <summary>
/// Component tests for the circle pages (FEAT-003), over the stub transport the account pages use.
/// </summary>
/// <remarks>
/// The themes are the ones TP-003 calls out as risky in the UI rather than in the API: the
/// deliberately uninformative invite confirmation, role-dependent rendering, and the destructive
/// confirmations.
/// </remarks>
public sealed class CirclePageTests
{
    private const string CirclesRoute = "api/v1/circles";

    private static CircleDetailResponse Circle(
        Guid id,
        string role,
        params CirclePersonResponse[] people) =>
        new(id, "Müller", "Familie", role, people);

    private static CirclePersonResponse Person(
        string name,
        string role = "Member",
        bool isManaged = false,
        bool isYou = false) =>
        new(Guid.CreateVersion7(), name, role, isManaged, isYou);

    // ---- the circle list (FR-016) ----

    [Fact]
    public async Task MyCircles_WithNoCircles_SaysSoRatherThanShowingAnEmptyList()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(HttpMethod.Get, CirclesRoute, Array.Empty<CircleSummaryResponse>());

        var page = context.RenderPage<MyCircles>();

        Assert.Contains("CirclesEmpty", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Circles are online-only (ADR-0033): an offline load must say the screen needs a connection —
    /// distinctly from a failure — and must not offer the create form, which would fail offline.
    /// </summary>
    [Fact]
    public async Task MyCircles_WhenOffline_ShowsTheConnectivityNotice_AndHidesTheCreateForm()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.Offline = true;

        var page = context.RenderPage<MyCircles>();

        Assert.Contains("CircleOfflineNotice", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CirclesCreateTitle", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The label is user data. Rendering it from the response — never from a resource lookup — is
    /// what stops a circle somebody named "Familie" turning into "Family" for an English viewer
    /// (FR-016).
    /// </summary>
    [Fact]
    public async Task MyCircles_ShowsTheStoredLabel_NotOneLookedUpForTheViewer()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(
            HttpMethod.Get,
            CirclesRoute,
            new[] { new CircleSummaryResponse(Guid.CreateVersion7(), "Müller", "Jassrunde", "Owner", 4) });

        var page = context.RenderPage<MyCircles>();
        var list = page.Find(".circles-list").InnerHtml;

        // Scoped to the list on purpose: the *create* form below it is legitimately prefilled with
        // the language default, and that is a different thing from re-labelling a stored circle.
        Assert.Contains("Jassrunde", list, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleLabelDefault", list, StringComparison.Ordinal);
    }

    /// <summary>
    /// A circle's size is a counted string, and a circle that was just created holds exactly one
    /// person — so the singular is the case this list shows most often, not an edge case.
    /// </summary>
    /// <remarks>
    /// Found on screen during the M2 browser walk-through: DE, EN and FR all inflect the noun, and
    /// one format string rendered "1 Personen", "1 people", "1 personnes". The fix is a second
    /// resource entry, and this asserts the page picks between them — the wording itself is a
    /// translation review.
    /// </remarks>
    [Theory]
    [InlineData(1, "CirclesPeopleCountOne")]
    [InlineData(4, "CirclesPeopleCount")]
    public async Task MyCircles_UsesTheSingularSizeString_OnlyForACircleOfOne(int people, string expectedKey)
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(
            HttpMethod.Get,
            CirclesRoute,
            new[] { new CircleSummaryResponse(Guid.CreateVersion7(), "Müller", "Familie", "Owner", people) });

        var list = context.RenderPage<MyCircles>().Find(".circles-list").InnerHtml;

        Assert.Contains(expectedKey, list, StringComparison.Ordinal);

        // The plural key is a prefix of the singular one, so "contains" alone cannot tell them
        // apart — at four people the singular entry must be absent outright.
        if (people > 1)
        {
            Assert.DoesNotContain("CirclesPeopleCountOne", list, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Badging all three roles makes the list noise; what the owner needs at a glance is who
    /// <em>else</em> can act (FEAT-003).
    /// </summary>
    [Fact]
    public async Task MyCircles_BadgesOwnerAndAdmin_ButNotPlainMembership()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(
            HttpMethod.Get,
            CirclesRoute,
            new[] { new CircleSummaryResponse(Guid.CreateVersion7(), "Müller", "Familie", "Member", 3) });

        var page = context.RenderPage<MyCircles>();

        Assert.DoesNotContain("CircleRoleMember", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleRoleOwner", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MyCircles_PrefillsTheLabelForTheCurrentLanguage()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(HttpMethod.Get, CirclesRoute, Array.Empty<CircleSummaryResponse>());

        var page = context.RenderPage<MyCircles>();

        // A default at creation time only. The pass-through localizer echoes the key, which is
        // enough to prove the field is seeded from the resource rather than left blank.
        Assert.Equal("CircleLabelDefault", page.Find("#circle-label").GetAttribute("value"));
    }

    [Fact]
    public async Task MyCircles_CreationRefusal_IsShownInTheUsersLanguage()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(HttpMethod.Get, CirclesRoute, Array.Empty<CircleSummaryResponse>());
        context.Api.RespondProblem(
            HttpMethod.Post,
            CirclesRoute,
            HttpStatusCode.Conflict,
            "owned_circle_limit_reached");

        var page = context.RenderPage<MyCircles>();
        page.Find("#circle-name").Input("Müller");
        page.Find("form").Submit();

        // The resource key, not the server's English title — that is the whole point of the code.
        Assert.Contains("CircleErrorOwnedLimit", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Refused.", page.Markup, StringComparison.Ordinal);
    }

    // ---- the circle detail page (FR-017, FR-019 to FR-022) ----

    private static AccountTestContext DetailContext(
        out Guid circleId,
        string role,
        params CirclePersonResponse[] people)
    {
        var context = new AccountTestContext();
        var id = Guid.CreateVersion7();
        circleId = id;

        context.SignIn();
        context.Api.RespondJson(HttpMethod.Get, $"{CirclesRoute}/{id}", Circle(id, role, people));
        context.Api.RespondJson(
            HttpMethod.Get,
            $"{CirclesRoute}/{id}/invitations",
            Array.Empty<PendingInvitationResponse>());

        return context;
    }

    private static IRenderedComponent<CircleDetail> RenderDetail(AccountTestContext context, Guid circleId) =>
        context.Render<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(
                parameters => parameters.AddChildContent<CircleDetail>(
                    child => child.Add(p => p.CircleId, circleId)))
            .FindComponent<CircleDetail>();

    /// <summary>
    /// The confirmation says nothing about whether a mail went out, whether the address already
    /// belongs to a member, or whether a decline is still in its cooldown. All three are the same
    /// sentence, and that is what keeps the invite form from being an enumeration oracle (NFR-005).
    /// </summary>
    [Fact]
    public async Task Invite_ConfirmsWithoutSayingWhetherAnythingWasSent()
    {
        await using var context = DetailContext(out var circleId, "Owner", Person("Anna", "Owner", isYou: true));
        context.Api.Respond(HttpMethod.Post, $"{CirclesRoute}/{circleId}/invitations", HttpStatusCode.Accepted);

        var page = RenderDetail(context, circleId);
        page.Find("#invite-email").Input("bea@example.test");
        page.FindAll("form").First(f => f.QuerySelector("#invite-email") is not null).Submit();

        Assert.Contains("CircleInviteSent", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// After a successful invite the emptied address field is back to its fresh state — not flagged
    /// "Required" right under the confirmation (BTP-3).
    /// </summary>
    [Fact]
    public async Task Invite_OnSuccess_ClearsTheFieldWithoutFlaggingItRequired()
    {
        await using var context = DetailContext(out var circleId, "Owner", Person("Anna", "Owner", isYou: true));
        context.Api.Respond(HttpMethod.Post, $"{CirclesRoute}/{circleId}/invitations", HttpStatusCode.Accepted);

        var page = RenderDetail(context, circleId);
        page.Find("#invite-email").Input("bea@example.test");
        page.Find("#invite-email").Blur();
        page.FindAll("form").First(f => f.QuerySelector("#invite-email") is not null).Submit();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("CircleInviteSent", page.Markup, StringComparison.Ordinal);
            Assert.Empty(page.Find("#invite-email").GetAttribute("value") ?? string.Empty);
            Assert.Empty(page.FindAll(".mud-input-error"));
        });
    }

    /// <summary>
    /// After adding an offline person the emptied name field is not flagged "Required" (BTP-3).
    /// </summary>
    [Fact]
    public async Task AddManaged_OnSuccess_ClearsTheFieldWithoutFlaggingItRequired()
    {
        await using var context = DetailContext(out var circleId, "Owner", Person("Anna", "Owner", isYou: true));
        context.Api.RespondJson(
            HttpMethod.Post,
            $"{CirclesRoute}/{circleId}/managed-members",
            Person("Opa", isManaged: true),
            HttpStatusCode.Created);

        var page = RenderDetail(context, circleId);
        page.Find("#managed-name").Input("Opa");
        page.Find("#managed-name").Blur();
        page.FindAll("form").First(f => f.QuerySelector("#managed-name") is not null).Submit();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("CircleSaved", page.Markup, StringComparison.Ordinal);
            Assert.Empty(page.Find("#managed-name").GetAttribute("value") ?? string.Empty);
            Assert.Empty(page.FindAll(".mud-input-error"));
        });
    }

    /// <summary>
    /// An offline person can be invited straight from their row, and the general invite form offers
    /// aiming an invitation at one — the UI half of FR-019's promotion the backend already supported.
    /// </summary>
    [Fact]
    public async Task OfflinePerson_OffersInvitingThemToJoin_AndRevealsTheAsThemForm()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Mia", "Member", isManaged: true));

        var page = RenderDetail(context, circleId);

        Assert.Contains("CircleInviteToJoin", page.Markup, StringComparison.Ordinal);
        Assert.Contains("CircleInviteFor", page.Markup, StringComparison.Ordinal);

        page.FindAll("button")
            .First(b => b.TextContent.Contains("CircleInviteToJoin", StringComparison.Ordinal))
            .Click();

        Assert.Contains("CircleInviteAsLead", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>A pending invitation aimed at an offline person says so, so an admin can tell it apart
    /// from an ordinary invitation (FR-019).</summary>
    [Fact]
    public async Task PendingInvitation_AimedAtAnOfflinePerson_ShowsWhoItIsFor()
    {
        await using var context = new AccountTestContext();
        var id = Guid.CreateVersion7();
        context.SignIn();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"{CirclesRoute}/{id}",
            Circle(id, "Owner", Person("Anna", "Owner", isYou: true), Person("Mia", "Member", isManaged: true)));
        context.Api.RespondJson(
            HttpMethod.Get,
            $"{CirclesRoute}/{id}/invitations",
            new[]
            {
                new PendingInvitationResponse(
                    Guid.CreateVersion7(), "mia@example.test",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(14),
                    Guid.CreateVersion7(), "Mia"),
            });

        var page = RenderDetail(context, id);

        Assert.Contains("CirclePendingAsTarget", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// When the circle has offline people and the invitation was left open, accepting first asks the
    /// recipient who they are joining as (FR-019, ADR-0043).
    /// </summary>
    [Fact]
    public async Task JoinRequest_ForACircleWithOfflinePeople_AsksWhoYouAreJoiningAs()
    {
        await using var context = new AccountTestContext();
        context.SignIn();

        var circleId = Guid.CreateVersion7();
        var invitationId = Guid.CreateVersion7();
        context.Api.RespondJson(
            HttpMethod.Get,
            "api/v1/invitations",
            new[] { new JoinRequestResponse(circleId, invitationId, "Müller", "Familie", "Anna") });
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/invitations/{circleId}/{invitationId}/targets",
            new JoinTargetsResponse(
                PreselectedMembershipId: null,
                PreselectedDisplayName: null,
                Options: new[] { new ManagedPersonOption(Guid.CreateVersion7(), "Mia") }));

        var page = context.RenderPage<JoinRequests>();
        page.FindAll("button")
            .First(b => b.TextContent.Contains("InvitationAccept", StringComparison.Ordinal))
            .Click();

        Assert.Contains("InvitationWhoAreYou", page.Markup, StringComparison.Ordinal);
        Assert.Contains("InvitationJoinAsNew", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMember_SeesNeitherTheInviteFormNorTheDeleteSection()
    {
        await using var context = DetailContext(out var circleId, "Member", Person("Anna", "Member", isYou: true));

        var page = RenderDetail(context, circleId);

        Assert.Empty(page.FindAll("#invite-email"));
        Assert.DoesNotContain("CircleDeleteTitle", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleManagedAdd", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>An admin administers, but deletion and role changes stay with the owner (FR-020).</summary>
    [Fact]
    public async Task AnAdmin_CanInviteButCannotDeleteTheCircleOrChangeRoles()
    {
        await using var context = DetailContext(
            out var circleId,
            "Admin",
            Person("Anna", "Owner"),
            Person("Bea", "Admin", isYou: true),
            Person("Chris"));

        var page = RenderDetail(context, circleId);

        Assert.NotEmpty(page.FindAll("#invite-email"));
        Assert.DoesNotContain("CircleDeleteTitle", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleMakeAdmin", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleTransfer", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only the owner removes an admin; an admin may remove plain members (FR-020). Getting this
    /// backwards renders a button the server would refuse, which reads as a broken product.
    /// </summary>
    [Fact]
    public async Task AnAdmin_MayRemoveAMemberButNotAnotherAdmin()
    {
        await using var context = DetailContext(
            out var circleId,
            "Admin",
            Person("Anna", "Owner"),
            Person("Bea", "Admin", isYou: true),
            Person("Chris", "Admin"),
            Person("Dora"));

        var page = RenderDetail(context, circleId);
        var rows = page.FindAll(".person-row");

        Assert.DoesNotContain("CircleRemove", RowFor(rows, "Chris"), StringComparison.Ordinal);
        Assert.Contains("CircleRemove", RowFor(rows, "Dora"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOwner_IsNeverOfferedAWayToRemoveThemselvesOrBeRemoved()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Bea"));

        var page = RenderDetail(context, circleId);
        var rows = page.FindAll(".person-row");

        Assert.DoesNotContain("CircleRemove", RowFor(rows, "Anna"), StringComparison.Ordinal);
        Assert.DoesNotContain("CircleMakeAdmin", RowFor(rows, "Anna"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AManagedMember_IsMarkedAndIsNeverOfferedARole()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Opa", isManaged: true));

        var page = RenderDetail(context, circleId);
        var row = RowFor(page.FindAll(".person-row"), "Opa");

        Assert.Contains("CircleManagedBadge", row, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleMakeAdmin", row, StringComparison.Ordinal);
        Assert.DoesNotContain("CircleTransfer", row, StringComparison.Ordinal);
        Assert.Contains("CircleManagedEdit", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-022: the threat is the misclick, so the button stays shut until the name is typed back.
    /// </summary>
    [Fact]
    public async Task DeleteButton_StaysDisabledUntilTheNameIsTypedBack()
    {
        await using var context = DetailContext(out var circleId, "Owner", Person("Anna", "Owner", isYou: true));

        var page = RenderDetail(context, circleId);
        var button = page.FindAll("button").First(b => b.TextContent.Contains("CircleDeleteSubmit", StringComparison.Ordinal));

        Assert.True(button.HasAttribute("disabled"));

        page.Find("#confirm-name").Input("Nope");
        Assert.True(page.FindAll("button")
            .First(b => b.TextContent.Contains("CircleDeleteSubmit", StringComparison.Ordinal))
            .HasAttribute("disabled"));

        page.Find("#confirm-name").Input("Müller");
        Assert.False(page.FindAll("button")
            .First(b => b.TextContent.Contains("CircleDeleteSubmit", StringComparison.Ordinal))
            .HasAttribute("disabled"));
    }

    /// <summary>
    /// The two refusals an owner meets when leaving each name a way out, and the page has to show
    /// which one applies rather than a generic conflict (FR-021).
    /// </summary>
    [Fact]
    public async Task AnOwnerLeaving_IsToldWhichWayOutTheyHave()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Bea"));

        context.Api.RespondProblem(
            HttpMethod.Post,
            $"{CirclesRoute}/{circleId}/leave",
            HttpStatusCode.Conflict,
            "owner_must_transfer_ownership");

        var page = RenderDetail(context, circleId);
        page.FindAll("button").First(b => b.TextContent.Contains("CircleLeave", StringComparison.Ordinal)).Click();
        page.FindAll("button").First(b => b.TextContent.Contains("CircleConfirmYes", StringComparison.Ordinal)).Click();

        Assert.Contains("CircleErrorOwnerMustTransfer", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// A destructive action asks first. The confirmation names the person, so "remove" cannot be
    /// confused with the row above it.
    /// </summary>
    [Fact]
    public async Task Removing_AsksFirst_AndNamesThePerson()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Bea"));

        var page = RenderDetail(context, circleId);
        page.FindAll("button").First(b => b.TextContent.Contains("CircleRemove", StringComparison.Ordinal)).Click();

        Assert.Contains("Bea", page.Markup, StringComparison.Ordinal);
        Assert.Contains("CircleConfirmYes", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// NFR-006's answer at the UI: not being in a circle is reported exactly as no such circle, and
    /// the page must not leak the difference either.
    /// </summary>
    [Fact]
    public async Task ACircleTheCallerIsNotIn_ReadsAsNoSuchCircle()
    {
        await using var context = new AccountTestContext();
        var circleId = Guid.CreateVersion7();
        context.SignIn();
        context.Api.RespondProblem(
            HttpMethod.Get,
            $"{CirclesRoute}/{circleId}",
            HttpStatusCode.NotFound,
            "circle_not_found");

        var page = RenderDetail(context, circleId);

        Assert.Contains("CircleErrorNotFound", page.Markup, StringComparison.Ordinal);
    }

    // ---- responding to an invitation (FR-018) ----

    [Fact]
    public async Task JoinRequests_WithNothingWaiting_SaysSo()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(HttpMethod.Get, "api/v1/invitations", Array.Empty<JoinRequestResponse>());

        var page = context.RenderPage<JoinRequests>();

        Assert.Contains("InvitationsEmpty", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JoinRequests_NamesTheCircleAndWhoInvitedYou()
    {
        await using var context = new AccountTestContext();
        context.SignIn();
        context.Api.RespondJson(
            HttpMethod.Get,
            "api/v1/invitations",
            new[]
            {
                new JoinRequestResponse(Guid.CreateVersion7(), Guid.CreateVersion7(), "Müller", "Familie", "Anna"),
            });

        var page = context.RenderPage<JoinRequests>();

        Assert.Contains("Müller", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Familie", page.Markup, StringComparison.Ordinal);

        // The inviter's name goes through a format string ("{0} invited you"), whose word order
        // differs per language. The pass-through localizer echoes the key without its placeholder,
        // so the name cannot appear here; that the sentence is rendered at all is what this asserts.
        // The real resources are covered by LocalizationParityTests and the browser walkthrough.
        Assert.Contains("InvitationFrom", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-021 asks the leave confirmation to say what leaving does <em>not</em> take: the account
    /// stays, and the user's own Secret-Santa groups, members and completed draws are untouched.
    /// </summary>
    [Fact]
    public async Task LeaveConfirmation_SaysWhatLeavingDoesNotTake()
    {
        await using var context = DetailContext(
            out var circleId,
            "Member",
            Person("Anna", "Owner"),
            Person("Bea", "Member", isYou: true));

        var page = RenderDetail(context, circleId);
        page.FindAll("button").First(b => b.TextContent.Contains("CircleLeave", StringComparison.Ordinal)).Click();

        var confirmation = page.Find(".mud-alert").TextContent;

        Assert.Contains("CircleLeaveConfirm", confirmation, StringComparison.Ordinal);
        Assert.Contains("CircleLeaveKeeps", confirmation, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-022's confirmation is specific, not merely stern: the circle by name, how many people lose
    /// access, and every managed member by name — they are the only data here that nobody else can
    /// recreate.
    /// </summary>
    [Fact]
    public async Task DeleteConfirmation_NamesTheCircle_CountsWhoLosesAccess_AndNamesEveryManagedMember()
    {
        await using var context = DetailContext(
            out var circleId,
            "Owner",
            Person("Anna", "Owner", isYou: true),
            Person("Bea", "Member"),
            Person("Grosi", isManaged: true),
            Person("Grosspapa", isManaged: true));

        var page = RenderDetail(context, circleId);
        var danger = page.Find(".circletools-danger");

        // The two managed members are named individually — the assertion that fails if the list is
        // ever replaced by a count, or moved inside the format string where a translation could drop
        // it.
        Assert.Contains("Grosi", danger.TextContent, StringComparison.Ordinal);
        Assert.Contains("Grosspapa", danger.TextContent, StringComparison.Ordinal);

        // The four sentences FR-022 requires. Two of them carry the circle's name and the count of
        // people losing access as placeholders, which the pass-through localizer drops — that the
        // placeholders survive translation is asserted in LocalizationParityTests, and the prose
        // itself in the browser walkthrough.
        Assert.Contains("CircleDeleteNames", danger.TextContent, StringComparison.Ordinal);
        Assert.Contains("CircleDeleteLosesAccess", danger.TextContent, StringComparison.Ordinal);
        Assert.Contains("CircleDeleteManagedNamed", danger.TextContent, StringComparison.Ordinal);
        Assert.Contains("CircleDeleteKeeps", danger.TextContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// A circle with nobody to lose but its owner has no managed members to name, and the sentence
    /// naming them is left out rather than rendered empty.
    /// </summary>
    [Fact]
    public async Task DeleteConfirmation_WithNoManagedMembers_LeavesThatSentenceOut()
    {
        await using var context = DetailContext(out var circleId, "Owner", Person("Anna", "Owner", isYou: true));

        var danger = RenderDetail(context, circleId).Find(".circletools-danger").TextContent;

        Assert.DoesNotContain("CircleDeleteManagedNamed", danger, StringComparison.Ordinal);
        Assert.Contains("CircleDeleteNames", danger, StringComparison.Ordinal);
    }

    // ---- the invitation link lands here (FR-018) ----

    /// <summary>
    /// Expired, revoked, already used and never existed are one answer, deliberately. The API gives
    /// the landing page no way to tell them apart, which is what stops a forwarded link from becoming
    /// a probe — and it means the page must say "no longer valid" plainly rather than guess why.
    /// </summary>
    [Fact]
    public async Task InvitationLanding_WhenTheInvitationIsNoLongerValid_SaysSoPlainly_WithoutSayingWhy()
    {
        await using var context = new AccountTestContext();
        context.Api.Respond(HttpMethod.Get, $"api/v1/invitations/{Token}", HttpStatusCode.NotFound);

        var page = RenderLanding(context);

        Assert.Contains("InvitationInvalid", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("InvitationAccept", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The NFR-005 property of this page: it offers signing in <strong>and</strong> signing up, and
    /// never says which one applies. Saying so would turn the invitation into an
    /// account-existence oracle — the leak ADR-0018 paid real UX to avoid.
    /// </summary>
    [Fact]
    public async Task InvitationLanding_ToSomeoneNotSignedIn_OffersBothWaysIn_WithoutSayingWhichApplies()
    {
        await using var context = new AccountTestContext();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/invitations/{Token}",
            new InvitationPreviewResponse("Müller", "Familie", "Anna"));

        var page = RenderLanding(context);

        Assert.Contains("InvitationLandingSignIn", page.Markup, StringComparison.Ordinal);
        Assert.Contains("InvitationLandingRegister", page.Markup, StringComparison.Ordinal);

        // Declining needs no account either, on purpose: requiring one would make a decline prove the
        // address has one (FR-018).
        Assert.Contains("InvitationDecline", page.Markup, StringComparison.Ordinal);

        // What the holder is shown is the circle, its label and who invited them — and nobody else in
        // it (FR-018's pre-accept visibility). The name and the inviter reach the lead sentence
        // through placeholders the pass-through localizer drops; the label is rendered as the user
        // data it is, so it appears verbatim here.
        Assert.Contains("InvitationLandingLead", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Familie", page.Markup, StringComparison.Ordinal);
    }

    /// <summary>Declining from the link needs no account, and says so once it is done.</summary>
    [Fact]
    public async Task InvitationLanding_Declining_NeedsNoAccount_AndConfirmsPlainly()
    {
        await using var context = new AccountTestContext();
        context.Api.RespondJson(
            HttpMethod.Get,
            $"api/v1/invitations/{Token}",
            new InvitationPreviewResponse("Müller", "Familie", "Anna"));
        context.Api.Respond(HttpMethod.Post, $"api/v1/invitations/{Token}/decline", HttpStatusCode.NoContent);

        var page = RenderLanding(context);
        page.FindAll("button").First(b => b.TextContent.Contains("InvitationDecline", StringComparison.Ordinal)).Click();

        Assert.Contains("InvitationDeclined", page.Markup, StringComparison.Ordinal);
        Assert.Contains($"POST api/v1/invitations/{Token}/decline", context.Api.Calls, StringComparer.Ordinal);
    }

    private const string Token = "s3cr3t-from-the-mail";

    private static IRenderedComponent<InvitationLanding> RenderLanding(AccountTestContext context) =>
        context.Render<Microsoft.AspNetCore.Components.Authorization.CascadingAuthenticationState>(
                parameters => parameters.AddChildContent<InvitationLanding>(
                    child => child.Add(p => p.Token, Token)))
            .FindComponent<InvitationLanding>();

    private static string RowFor(IReadOnlyList<AngleSharp.Dom.IElement> rows, string displayName) =>
        rows.First(r => r.TextContent.Contains(displayName, StringComparison.Ordinal)).InnerHtml;
}
