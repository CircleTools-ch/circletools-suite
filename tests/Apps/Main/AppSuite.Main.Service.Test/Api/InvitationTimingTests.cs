using System.Diagnostics;
using System.Net.Http.Json;
using AppSuite.Circles.Contracts;
using AppSuite.Main.Contracts;

namespace AppSuite.Main.Service.Test.Api;

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
    /// <summary>How far a response may sit from the budget before the timer stops being the excuse.</summary>
    private static readonly TimeSpan TimerJitter = TimeSpan.FromMilliseconds(2);
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

        var durations = new Dictionary<string, List<double>>
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
                var elapsed = await TimeAsync(() => InviteAsync(owner, circle.Id, email));

                if (measured)
                {
                    durations[arm].Add(elapsed);
                }
            }
        }

        // The fastest sample each arm managed, not its median. The constant-time budget is a floor
        // that a busy machine can only push a sample above, so the minimum is the statistic load
        // cannot inflate — and load does reach this test: run alongside the account collection, one
        // arm's median hit 135 ms against a 120 ms budget with nothing wrong.
        var fastestPerArm = durations.ToDictionary(pair => pair.Key, pair => pair.Value.Min());
        var slowest = fastestPerArm.MaxBy(pair => pair.Value);
        var fastest = fastestPerArm.MinBy(pair => pair.Value);
        var difference = slowest.Value - fastest.Value;
        var budget = ConstantTimeFilter.DefaultBudget.TotalMilliseconds;
        var report = string.Join(". ", fastestPerArm.Select(m => $"{m.Key}: {m.Value:F2} ms"));

        // Asserted as "every arm reaches the budget" rather than "the arms are close", because the
        // leak here is too small to resolve against timer jitter — 1.05 ms of real difference on a
        // fastest-sample basis. Removing the filter drops every arm to ~15 ms, which is not arguable.
        Assert.True(
            fastest.Value >= budget - TimerJitter.TotalMilliseconds,
            $"NFR-005: the invite form (FR-017) is not being held to its constant-time budget. "
            + $"{report}. The budget is {budget:F0} ms, and '{fastest.Key}' came in under it — the "
            + "route has lost RequireConstantTime, and who is already in a circle, or who declined, "
            + "is timeable again.");

        Assert.True(
            difference <= TimerJitter.TotalMilliseconds,
            $"NFR-005: the invite form (FR-017) diverges by more than timer jitter under its budget. "
            + $"{report}. Slowest is '{slowest.Key}', fastest is '{fastest.Key}' — {difference:F2} ms "
            + $"apart over {Samples} interleaved samples. The budget has stopped covering the slowest "
            + "arm; raise ConstantTimeFilter.DefaultBudget rather than this threshold.");
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

    private static async Task<double> TimeAsync(Func<Task> call)
    {
        var stopwatch = Stopwatch.StartNew();
        await call();
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }
}
