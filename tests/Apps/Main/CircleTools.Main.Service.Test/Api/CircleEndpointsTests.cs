using System.Net;
using System.Net.Http.Json;
using CircleTools.Circles.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Covers the circle endpoints against the real stack — routing, the role filter, EF and Postgres
/// (FEAT-003, FR-016).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class CircleEndpointsTests(MailApiFactory factory)
{
    [Fact]
    public async Task Create_ReturnsTheCircleWithItsOwner()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");

        var response = await anna.Client.PostAsJsonAsync(
            "/api/v1/circles",
            new CreateCircleRequest("Müller", "Familie"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var circle = await response.Content.ReadFromJsonAsync<CircleDetailResponse>();

        Assert.NotNull(circle);
        Assert.Equal("Müller", circle.Name);
        Assert.Equal("Familie", circle.Label);
        Assert.Equal("owner", circle.Role);

        var person = Assert.Single(circle.People);
        Assert.Equal("Anna", person.DisplayName);
        Assert.Equal("owner", person.Role);
        Assert.False(person.IsManaged);
        Assert.True(person.IsYou);
    }

    [Fact]
    public async Task Create_ThenTheCircleIsInTheOwnersList()
    {
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var created = await (await bea.Client.PostAsJsonAsync(
                "/api/v1/circles",
                new CreateCircleRequest("Kegelclub", "Crew")))
            .Content.ReadFromJsonAsync<CircleDetailResponse>();

        var circles = await bea.Client.GetFromJsonAsync<IReadOnlyList<CircleSummaryResponse>>("/api/v1/circles");

        Assert.NotNull(circles);
        var summary = Assert.Single(circles, c => c.Id == created!.Id);
        Assert.Equal("Kegelclub", summary.Name);
        Assert.Equal("Crew", summary.Label);
        Assert.Equal("owner", summary.Role);
        Assert.Equal(1, summary.PeopleCount);
    }

    /// <summary>
    /// The label is user data. A viewer in another language sees the word the circle was given, not
    /// a translation of it (FR-016) — which is why no code path ever looks a label up in a resource
    /// file.
    /// </summary>
    [Fact]
    public async Task Label_IsNotRetranslatedForAViewerInAnotherLanguage()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna", "de-CH");
        var claude = await CircleTestAccounts.SignUpAsync(factory, "Claude", "fr-CH");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, claude, circle.Id);

        var seenByClaude = await claude.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circle.Id}");

        Assert.NotNull(seenByClaude);
        Assert.Equal("Familie", seenByClaude.Label);
        Assert.Equal("member", seenByClaude.Role);
    }

    /// <summary>
    /// Leaving needs no permission, only membership (FR-021): the membership ends, the circle drops
    /// out of the leaver's list, and it keeps its owner.
    /// </summary>
    [Fact]
    public async Task Leave_DeletesTheMembership_AndTheCircleVanishesFromTheirList()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        var left = await bea.Client.PostAsync(new Uri($"/api/v1/circles/{circle.Id}/leave", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);

        var beasCircles = await bea.Client.GetFromJsonAsync<IReadOnlyList<CircleSummaryResponse>>("/api/v1/circles");
        Assert.DoesNotContain(circle.Id, beasCircles!.Select(c => c.Id));

        // Anna's side: Bea is gone from the people list, and the circle still has exactly one owner.
        var seenByAnna = await anna.Client.GetFromJsonAsync<CircleDetailResponse>($"/api/v1/circles/{circle.Id}");
        Assert.DoesNotContain("Bea", seenByAnna!.People.Select(p => p.DisplayName));
        Assert.Single(seenByAnna.People, p => p.Role == "owner");
    }

    /// <summary>
    /// FR-021's "access ends immediately" is a claim about the token the leaver is still holding: no
    /// role and no membership ever enters an access token, so the next request is refused without any
    /// sign-out.
    /// </summary>
    [Fact]
    public async Task Leave_ThenARequestWithAStillValidToken_IsRefused()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");
        await CircleFixture.AddMemberAsync(factory, anna, bea, circle.Id);

        await bea.Client.PostAsync(new Uri($"/api/v1/circles/{circle.Id}/leave", UriKind.Relative), content: null);

        var afterLeaving = await bea.Client.GetAsync(new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative));

        // Not-Found, the answer a stranger gets: having been in a circle is not a reason to be told
        // it still exists (NFR-006).
        Assert.Equal(HttpStatusCode.NotFound, afterLeaving.StatusCode);
    }

    /// <summary>
    /// FR-022: a pending invitation dies with its circle, and the person holding it is told the
    /// circle no longer exists rather than meeting an error.
    /// </summary>
    [Fact]
    public async Task WhenTheCircleIsDeleted_ItsPendingInvitationIsGone_AndTheRecipientIsToldSo()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");

        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");

        var invited = await anna.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circle.Id}/invitations",
            new InviteRequest(bea.Email, ManagedMembershipId: null));

        invited.EnsureSuccessStatusCode();
        await CircleTestAccounts.DispatchOutboxAsync(factory);

        var waiting = await bea.Client.GetFromJsonAsync<IReadOnlyList<JoinRequestResponse>>("/api/v1/invitations");
        var request = waiting!.Single(r => r.CircleId == circle.Id);

        var deleted = await anna.Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            new Uri($"/api/v1/circles/{circle.Id}", UriKind.Relative))
        {
            Content = JsonContent.Create(new DeleteCircleRequest("Müller")),
        });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // The join request is simply absent — the invitation went with the circle.
        var afterwards = await bea.Client.GetFromJsonAsync<IReadOnlyList<JoinRequestResponse>>("/api/v1/invitations");
        Assert.DoesNotContain(circle.Id, afterwards!.Select(r => r.CircleId));

        // And accepting it now says the circle is gone, not that something went wrong.
        var accepted = await bea.Client.PostAsync(
            new Uri($"/api/v1/invitations/{circle.Id}/{request.InvitationId}/accept", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, accepted.StatusCode);
    }

    /// <summary>
    /// An unverified account gets nothing account-scoped, and the refusal carries a code the client
    /// can turn into an offer to resend the verification mail (FR-016).
    /// </summary>
    [Fact]
    public async Task Create_UnverifiedAccount_IsRefusedWithAResendOffer()
    {
        var client = factory.CreateClient();
        var unverified = await CircleTestAccounts.SignUpUnverifiedAsync(factory, client, "Dora");

        client.DefaultRequestHeaders.Authorization = unverified;

        var response = await client.PostAsJsonAsync("/api/v1/circles", new CreateCircleRequest("Müller", "Familie"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            "email_not_verified",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }
}
