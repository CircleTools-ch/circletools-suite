using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The timing half of NFR-005: an address-accepting endpoint must not answer measurably faster for
/// an address that has no account than for one that does.
/// </summary>
/// <remarks>
/// <para>
/// This is the failure mode ordinary functional tests cannot see. `EnumerationTests`-style checks
/// compare body and status, and a leaky endpoint passes every one of them while still telling an
/// attacker, in milliseconds, which addresses are registered.
/// </para>
/// <para>
/// <strong>How it measures.</strong> Samples are <em>interleaved</em> — one of each path per round —
/// so JIT warm-up, connection-pool growth and background dispatcher work land on both paths equally
/// rather than on whichever ran first. Medians are compared, not means, because a single scheduling
/// hiccup would otherwise decide the result. A warm-up round runs first and is discarded.
/// </para>
/// <para>
/// <strong>What it will and will not catch.</strong> The threshold is deliberately loose: this
/// catches a <em>structural</em> asymmetry — a password hash, a mail send or a token generation on
/// one path only — which is what NFR-005 is actually about. It is not a rigorous side-channel
/// analysis and cannot catch a difference of tens of microseconds. The structural guarantees
/// (<c>OutboxTests.MailIsEnqueuedNotAwaited</c> and the decoy verification in the login endpoint)
/// remain the real defence; this is the test that notices when one of them is removed.
/// </para>
/// <para>
/// <strong>The noise floor is a hole, and it has already let one through.</strong> A difference under
/// <see cref="NoiseFloor"/> passes whatever the ratio says, and <c>/forgot-password</c> sat in that
/// hole for the whole of M1: 2.36 ms against 1.24 ms on an idle machine — a 1.9x leak, 1.12 ms wide,
/// green every time it was run alone. It only failed once a loaded full-suite run scaled both paths
/// and pushed the gap past 3 ms. Two lessons are worth keeping: a stable ratio across differently
/// loaded runs is evidence of a structural asymmetry even when the absolute difference is small, and
/// this suite is more trustworthy under load than on an idle machine.
/// </para>
/// <para>
/// <strong>Two kinds of surface, two assertions.</strong> Sign-up and sign-in are equalised by
/// <c>DecoyPasswordVerifier</c> and keep the honest variance of the work they do, so they are judged
/// on the ratio between their medians. The rest carry <c>RequireConstantTime</c> and are judged on
/// whether every path actually reaches the budget — <see cref="AssertHeldToBudget"/> explains why
/// measuring their difference instead does not work.
/// </para>
/// </remarks>
[Collection(AccountApi.CollectionName)]
public sealed class EnumerationTimingTests(AccountApiFactory factory)
{
    /// <summary>Samples taken per path, after the warm-up round.</summary>
    private const int Samples = 25;

    /// <summary>Rounds run and discarded before measuring.</summary>
    private const int WarmupRounds = 5;

    /// <summary>
    /// How much slower one path may be than the other before this is called a leak. Applies only to
    /// routes <em>not</em> held to a constant-time budget.
    /// </summary>
    /// <remarks>
    /// A PBKDF2 verification, the cheapest realistic structural asymmetry, costs far more than this
    /// against an in-process test server. The absolute floor below keeps sub-millisecond scheduling
    /// noise from failing the build on a loaded CI machine.
    /// </remarks>
    private const double MaxRatio = 1.5;

    /// <summary>
    /// Differences below this are noise whatever the ratio says. Applies only to the
    /// decoy-equalised routes, which are the only ones still judged on a difference at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was 3 ms, and 3 ms was <strong>the bug</strong>: <c>/forgot-password</c> leaked at a
    /// steady 1.9x for the whole of M1 while its absolute gap — 1.12 ms — sat quietly underneath.
    /// A floor is a hole, and anything that fits through it ships.
    /// </para>
    /// <para>
    /// The two routes it still governs are sign-up and sign-in, which cost tens of milliseconds of
    /// deliberate password hashing. Against medians that size, half a millisecond of scheduling
    /// wobble is a rounding error and the ratio carries the assertion; the floor is only there so a
    /// pathologically fast pair cannot produce a meaningless ratio. The constant-time routes are not
    /// judged this way at all — see <see cref="AssertHeldToBudget"/> for why a difference threshold
    /// cannot work there.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan NoiseFloor = TimeSpan.FromMilliseconds(0.5);

    /// <summary>
    /// How far a constant-time response may sit from its budget before something other than the
    /// timer is responsible.
    /// </summary>
    /// <remarks>
    /// <c>Task.Delay</c> does not land exactly on its target — measured within about a millisecond
    /// either side here, occasionally finishing marginally early. This is that slack and nothing
    /// more: it is not a leak tolerance, because the assertion it guards is "the budget is in force",
    /// which fails by ~118 ms when it is not.
    /// </remarks>
    private static readonly TimeSpan TimerJitter = TimeSpan.FromMilliseconds(2);

    private const string ValidPassword = "correct-horse-battery";

    private static string UniqueEmail() => $"timing-{Guid.CreateVersion7():N}@example.test";

    [Fact]
    public async Task SignUp_AnswersInTheSameTimeForATakenAddressAsForAFreeOne()
    {
        var taken = await RegisteredAddressAsync();

        await CompareAsync(
            "sign-up (FR-007)",
            existing: () => RegisterAsync(taken),
            unknown: () => RegisterAsync(UniqueEmail()));
    }

    [Fact]
    public async Task ForgotPassword_AnswersInTheSameTimeWhetherOrNotTheAddressHasAnAccount()
    {
        var known = await RegisteredAddressAsync();

        await CompareAsync(
            "forgot-password (FR-009)",
            existing: () => ForgotAsync(known),
            unknown: () => ForgotAsync(UniqueEmail()),
            constantTime: true);
    }

    [Fact]
    public async Task SignIn_AnswersInTheSameTimeForAnUnknownAddressAsForAWrongPassword()
    {
        // The one most likely to leak: a real account pays for a password hash verification, and an
        // unknown address has no hash to verify. Without a decoy verification on the miss path the
        // two differ by the entire cost of PBKDF2 — which is, by design, a lot.
        var known = await RegisteredAddressAsync();

        await CompareAsync(
            "sign-in (FR-008)",
            existing: () => LoginAsync(known, "the-wrong-passphrase-entirely"),
            unknown: () => LoginAsync(UniqueEmail(), "the-wrong-passphrase-entirely"));
    }

    [Fact]
    public async Task ResendVerification_AnswersInTheSameTimeForAnUnknownAddressAsForAPendingOne()
    {
        // A registered-but-unconfirmed address is the branch that generates a token and queues a
        // mail; an unknown one does neither. The endpoint's own remark calls "already verified"
        // exactly the fact an attacker wants — and that branch is silent too, so all three have to
        // cost the same. Only two are compared here: confirming an address needs the mail sink, and
        // this collection has none. InvitationTimingTests covers the three-arm case with one.
        var pending = await RegisteredAddressAsync();

        await CompareAsync(
            "resend-verification (FR-007)",
            existing: () => ResendAsync(pending),
            unknown: () => ResendAsync(UniqueEmail()),
            constantTime: true);
    }

    [Fact]
    public async Task ChangeEmail_AnswersInTheSameTimeForATakenAddressAsForAFreeOne()
    {
        // Inverted against the others: here the *free* address is the expensive path — a token plus
        // two outbox rows — and a taken one returns having done nothing. Authenticated, so the
        // attacker needs an account of their own; that lowers the value of the oracle without
        // removing it, since the addresses it discloses are other people's.
        using var client = await SignedInClientAsync();
        var taken = await RegisteredAddressAsync();

        await CompareAsync(
            "change-email (FR-013)",
            existing: () => ChangeEmailAsync(client, taken),
            unknown: () => ChangeEmailAsync(client, UniqueEmail()),
            constantTime: true);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var email = await RegisteredAddressAsync();
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, ValidPassword));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return client;
    }

    private static async Task ChangeEmailAsync(HttpClient client, string newEmail)
    {
        using var _ = await client.PostAsJsonAsync(
            "/api/v1/account/change-email", new ChangeEmailRequest(newEmail));
    }

    private async Task ResendAsync(string email)
    {
        using var client = factory.CreateClient();
        using var _ = await client.PostAsJsonAsync(
            "/api/v1/account/resend-verification", new ResendVerificationRequest(email));
    }

    private async Task<string> RegisteredAddressAsync()
    {
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "de-CH"));
        response.EnsureSuccessStatusCode();

        return email;
    }

    private async Task RegisterAsync(string email)
    {
        using var client = factory.CreateClient();
        using var _ = await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "de-CH"));
    }

    private async Task ForgotAsync(string email)
    {
        using var client = factory.CreateClient();
        using var _ = await client.PostAsJsonAsync(
            "/api/v1/account/forgot-password", new ForgotPasswordRequest(email));
    }

    private async Task LoginAsync(string email, string password)
    {
        using var client = factory.CreateClient();
        using var _ = await client.PostAsJsonAsync(
            "/api/v1/account/login", new LoginRequest(email, password));
    }

    /// <summary>
    /// Runs both paths interleaved and fails if their median durations diverge.
    /// </summary>
    /// <param name="surface">The endpoint under test, for the failure message.</param>
    /// <param name="existing">Calls the endpoint with an address that has an account.</param>
    /// <param name="unknown">Calls it with one that does not.</param>
    /// <param name="constantTime">
    /// <see langword="true"/> when the route carries <c>RequireConstantTime</c>, which is judged on
    /// the absolute spread alone.
    /// </param>
    /// <remarks>
    /// <para>
    /// <strong>Two instruments, because one does not fit both kinds of endpoint.</strong> A route
    /// held to a fixed budget should show no spread at all — measured at 0.08–0.21 ms across the
    /// three — so it is judged absolutely, and the ratio escape hatch is withdrawn. That matters:
    /// the invite endpoint's real leak was 2.79 ms wide but only 1.18x, because its own baseline
    /// work is heavy enough to dilute the ratio past any threshold worth setting. A ratio is the
    /// wrong instrument for an expensive endpoint.
    /// </para>
    /// <para>
    /// A route equalised by <c>DecoyPasswordVerifier</c> instead of a budget keeps the honest
    /// variance of the work it really does — sign-up measures ~3.7 ms of it, creating a user row
    /// against queueing a notice — so there the ratio is the instrument that means something.
    /// </para>
    /// </remarks>
    private static async Task CompareAsync(
        string surface,
        Func<Task> existing,
        Func<Task> unknown,
        bool constantTime = false)
    {
        for (var i = 0; i < WarmupRounds; i++)
        {
            await existing();
            await unknown();
        }

        var existingTimes = new List<double>(Samples);
        var unknownTimes = new List<double>(Samples);

        for (var i = 0; i < Samples; i++)
        {
            // Alternate the order so neither path systematically benefits from a warm cache.
            if (i % 2 == 0)
            {
                existingTimes.Add(await TimeAsync(existing));
                unknownTimes.Add(await TimeAsync(unknown));
            }
            else
            {
                unknownTimes.Add(await TimeAsync(unknown));
                existingTimes.Add(await TimeAsync(existing));
            }
        }

        // A constant-time route is judged on the fastest sample each path managed, not the median.
        // The budget is a floor that load can only push a sample above, never below, so the minimum
        // is the one statistic a busy machine cannot inflate — and a full-suite run demonstrated the
        // need: contention pushed one arm's median to 135 ms against a 120 ms budget and failed a
        // route that has no leak. Minima are unmoved by that while still separating by the full
        // structural difference when the envelope is removed.
        if (constantTime)
        {
            AssertHeldToBudget(surface, existingTimes, unknownTimes);
            return;
        }

        var existingMedian = Median(existingTimes);
        var unknownMedian = Median(unknownTimes);

        var slower = Math.Max(existingMedian, unknownMedian);
        var faster = Math.Min(existingMedian, unknownMedian);
        var difference = slower - faster;
        var ratio = faster <= 0 ? 1 : slower / faster;

        Assert.True(
            difference <= NoiseFloor.TotalMilliseconds || ratio <= MaxRatio,
            $"NFR-005: {surface} is timing-distinguishable. "
            + $"Existing address: {existingMedian:F2} ms median. "
            + $"Unknown address: {unknownMedian:F2} ms median. "
            + $"That is a {ratio:F2}x difference ({difference:F2} ms) over {Samples} interleaved samples — "
            + "an attacker can tell which addresses have accounts by timing alone.");
    }

    /// <summary>
    /// Asserts that a route carrying <c>RequireConstantTime</c> really is pinned to its budget.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Why this and not a tighter spread threshold.</strong> Comparing the two paths directly
    /// runs into a resolution wall: <c>Task.Delay</c> lands within about a millisecond of its target,
    /// and the structural leaks this suite has actually caught were 0.70 ms (forgot-password),
    /// 1.05 ms (the invite form) and 3.01 ms (change-email). Timer jitter and the smallest real leak
    /// overlap, so no spread threshold separates them — one that catches 0.70 ms also fails on a
    /// quiet Tuesday.
    /// </para>
    /// <para>
    /// So the assertion changes shape. Rather than measure a difference too small to resolve, it
    /// checks the property that makes the difference unobservable in the first place: <em>every</em>
    /// path takes at least the budget. Remove the filter and both paths drop to single-digit
    /// milliseconds, roughly 118 ms below the line, which no jitter explains. That failure is
    /// unmissable where a sub-millisecond one was arguable — and it stays unmissable if the leak the
    /// filter hides ever grows.
    /// </para>
    /// </remarks>
    private static void AssertHeldToBudget(string surface, List<double> existingTimes, List<double> unknownTimes)
    {
        var budget = ConstantTimeFilter.DefaultBudget.TotalMilliseconds;
        var existingFloor = existingTimes.Min();
        var unknownFloor = unknownTimes.Min();
        var lowest = Math.Min(existingFloor, unknownFloor);

        // The minimum, because the budget is a floor: load pushes samples above it and nothing
        // pushes them below, so it is the one statistic a busy machine cannot inflate.
        Assert.True(
            lowest >= budget - TimerJitter.TotalMilliseconds,
            $"NFR-005: {surface} is not being held to its constant-time budget. "
            + $"Fastest with an account: {existingFloor:F2} ms. Fastest without: {unknownFloor:F2} ms. "
            + $"The budget is {budget:F0} ms. Either the route lost its RequireConstantTime, or the "
            + "filter is no longer running outermost — and the work behind it is timeable again.");

        var difference = Math.Abs(existingFloor - unknownFloor);

        Assert.True(
            difference <= TimerJitter.TotalMilliseconds,
            $"NFR-005: {surface} diverges by more than timer jitter even under its budget. "
            + $"Fastest with an account: {existingFloor:F2} ms. Fastest without: {unknownFloor:F2} ms — "
            + $"a {difference:F2} ms difference. The budget has stopped covering the slower path; "
            + "raise ConstantTimeFilter.DefaultBudget rather than this threshold.");
    }

    private static async Task<double> TimeAsync(Func<Task> call)
    {
        var stopwatch = Stopwatch.StartNew();
        await call();
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2
            : sorted[middle];
    }
}
