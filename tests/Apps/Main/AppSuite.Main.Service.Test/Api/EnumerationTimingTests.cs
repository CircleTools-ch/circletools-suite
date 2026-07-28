using System.Diagnostics;
using System.Net.Http.Json;
using AppSuite.Main.Contracts;

namespace AppSuite.Main.Service.Test.Api;

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
/// </remarks>
[Collection(AccountApi.CollectionName)]
public sealed class EnumerationTimingTests(AccountApiFactory factory)
{
    /// <summary>Samples taken per path, after the warm-up round.</summary>
    private const int Samples = 25;

    /// <summary>Rounds run and discarded before measuring.</summary>
    private const int WarmupRounds = 5;

    /// <summary>
    /// How much slower one path may be than the other before this is called a leak.
    /// </summary>
    /// <remarks>
    /// A PBKDF2 verification, the cheapest realistic structural asymmetry, costs far more than this
    /// against an in-process test server. The absolute floor below keeps sub-millisecond scheduling
    /// noise from failing the build on a loaded CI machine.
    /// </remarks>
    private const double MaxRatio = 1.5;

    /// <summary>Differences below this are noise whatever the ratio says.</summary>
    private static readonly TimeSpan NoiseFloor = TimeSpan.FromMilliseconds(3);

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
            unknown: () => ForgotAsync(UniqueEmail()));
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
    private static async Task CompareAsync(string surface, Func<Task> existing, Func<Task> unknown)
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
