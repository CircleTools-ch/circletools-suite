using System.Net.Http.Json;
using AppSuite.Circles.Contracts;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>Builds circles through the real endpoints, so the tests never seed the database.</summary>
internal static class CircleFixture
{
    /// <summary>Creates a circle owned by an account.</summary>
    /// <param name="owner">The account creating it.</param>
    /// <param name="name">The circle's name.</param>
    /// <param name="label">The circle's label.</param>
    /// <returns>The created circle.</returns>
    public static async Task<CircleDetailResponse> CreateAsync(
        CircleTestAccount owner,
        string name,
        string? label = null)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/v1/circles", new CreateCircleRequest(name, label));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CircleDetailResponse>())!;
    }

    /// <summary>
    /// Puts an account into a circle the way a person actually gets there: invited by an admin, then
    /// accepting the join request that appears in their own app — no token from any mail.
    /// </summary>
    /// <param name="factory">The API fixture, to drain the outbox.</param>
    /// <param name="inviter">The owner or admin sending the invitation.</param>
    /// <param name="invitee">The account joining.</param>
    /// <param name="circleId">The circle.</param>
    /// <returns>A task that completes once the membership exists.</returns>
    public static async Task AddMemberAsync(
        MailApiFactory factory,
        CircleTestAccount inviter,
        CircleTestAccount invitee,
        Guid circleId)
    {
        var invited = await inviter.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circleId}/invitations",
            new InviteRequest(invitee.Email, ManagedMembershipId: null));

        invited.EnsureSuccessStatusCode();
        await CircleTestAccounts.DispatchOutboxAsync(factory);

        var requests = await invitee.Client
            .GetFromJsonAsync<IReadOnlyList<JoinRequestResponse>>("/api/v1/invitations");

        var request = requests!.Single(r => r.CircleId == circleId);

        var accepted = await invitee.Client.PostAsync(
            new Uri($"/api/v1/invitations/{circleId}/{request.InvitationId}/accept", UriKind.Relative),
            content: null);

        accepted.EnsureSuccessStatusCode();
    }
}
