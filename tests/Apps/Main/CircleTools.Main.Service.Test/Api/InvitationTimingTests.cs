using System.Net.Http.Json;
using CircleTools.Circles.Contracts;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The timing half of NFR-005 for the invite form (FR-017) — the fifth address-accepting surface,
/// and the one ADR-0018 paid the most UX for.
/// </summary>
/// <remarks>
/// <para>
/// Three submissions must be indistinguishable: an address with no account, an address with one, and
/// an address already in the circle. The first two are symmetric by construction —
/// <c>InviteToCircleCommandHandler</c> performs the same two directory lookups and the same save
/// whichever it is, and only the mail payload differs. The third is not: an address already in the
/// circle (like one inside the silent decline cooldown) creates no invitation and queues no mail,
/// and the handler's own remark conceded that difference and called it "in the noise". This class is
/// what stops that from being an assumption.
/// </para>
/// <para>
/// Why it matters more here than on the account endpoints: those disclose whether an address is
/// registered, which is bad. This one discloses <em>who is in a family</em> to someone who is merely
/// an admin of some other circle — and, through the cooldown branch, whether a particular person
/// declined. That second fact is the one FR-018 spends a deliberately confusing admin experience to
/// keep hidden, so leaking it by stopwatch would waste the whole trade.
/// </para>
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class InvitationTimingTests(MailApiFactory factory)
{
    private const int Samples = 10;
    private const int WarmupRounds = 2;
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task Invite_NoAccount_WithAccount_AndAlreadyAMember_AreIndistinguishable()
    {
        var owner = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var insider = await CircleTestAccounts.SignUpAsync(factory, "Beat");
        var circle = await CircleFixture.CreateAsync(owner, "Müller", "Familie");

        await CircleFixture.AddMemberAsync(factory, owner, insider, circle.Id);

        // The with-account arm needs a fresh address every sample: invited twice, an address takes
        // the resend path instead, which is a different branch and would measure something else.
        var registered = await VerifiedAddressPoolAsync(WarmupRounds + Samples);

        var samples = new Dictionary<string, List<ConstantTimeSample>>
        {
            ["no account"] = [],
            ["with an account"] = [],
            ["already a member"] = [],
        };

        for (var round = 0; round < WarmupRounds + Samples; round++)
        {
            var measured = round >= WarmupRounds;

            // Rotate the order so no arm systematically runs on a warm connection.
            var arms = new (string Arm, string Email)[]
            {
                ("no account", $"nobody-{Guid.CreateVersion7():N}@example.test"),
                ("with an account", registered[round]),
                ("already a member", insider.Email),
            };

            foreach (var (arm, email) in arms.Skip(round % 3).Concat(arms.Take(round % 3)))
            {
                var sample = await factory.ConstantTimeClock.MeasureAsync(() => InviteAsync(owner, circle.Id, email));

                if (measured)
                {
                    samples[arm].Add(sample);
                }
            }
        }

        // Removing the filter drops every arm to ~15 ms, which is not arguable. If it fails here, who
        // is already in a circle, or who declined, is timeable again.
        ConstantTimeEnvelope.AssertHeldToBudget("the invite form (FR-017)", samples);
    }

    private static async Task InviteAsync(CircleTestAccount inviter, Guid circleId, string email)
    {
        using var response = await inviter.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circleId}/invitations",
            new InviteRequest(email, ManagedMembershipId: null));

        // Every arm must be a success — a refusal takes a different path and would compare nothing.
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Registers and confirms <paramref name="count"/> accounts, so their addresses resolve through
    /// <c>IAccountDirectory.FindByVerifiedEmailAsync</c>. They are never signed in — the invite
    /// handler only asks whether the address belongs to a verified account.
    /// </summary>
    private async Task<IReadOnlyList<string>> VerifiedAddressPoolAsync(int count)
    {
        var addresses = new List<string>(count);
        using var client = factory.CreateClient();

        for (var i = 0; i < count; i++)
        {
            var email = $"pooled-{Guid.CreateVersion7():N}@example.test";

            using var registered = await client.PostAsJsonAsync(
                "/api/v1/account/register",
                new RegisterRequest("Pooled", email, Password, "de-CH"));

            registered.EnsureSuccessStatusCode();
            addresses.Add(email);
        }

        // One dispatch for the whole pool rather than one each: the outbox is the slow part.
        await CircleTestAccounts.DispatchOutboxAsync(factory);

        foreach (var email in addresses)
        {
            var mail = await factory.WaitForMailAsync(email);
            var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
            var (userId, token) = LinkParser.Parse(body);

            using var verified = await client.PostAsJsonAsync(
                "/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

            verified.EnsureSuccessStatusCode();
        }

        return addresses;
    }
}
