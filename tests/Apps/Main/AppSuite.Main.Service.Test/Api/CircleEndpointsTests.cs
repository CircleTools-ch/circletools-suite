using System.Net;
using System.Net.Http.Json;
using AppSuite.Circles.Contracts;

namespace AppSuite.Main.Service.Test.Api;

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
