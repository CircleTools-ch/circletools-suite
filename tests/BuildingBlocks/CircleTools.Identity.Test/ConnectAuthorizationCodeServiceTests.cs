using CircleTools.Identity.Tokens;

namespace CircleTools.Identity.Test;

/// <summary>
/// Covers the security envelope of the SSO authorization code (FR-031, ADR-0039): single-use,
/// short-lived, PKCE-bound, and stored only as a handle. Time is supplied explicitly, so expiry is
/// exercised without any wall-clock waiting.
/// </summary>
public sealed class ConnectAuthorizationCodeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("0198a1f0-0000-7000-8000-0000000000aa");

    private const string Verifier = "the-clients-in-memory-verifier-value";
    private const string Client = "secretsanta-web";
    private const string ReturnUri = "https://sibling.test/account/connect/callback";

    private static (ConnectAuthorizationCodeService Service, TestClock Clock) NewService()
    {
        var clock = new TestClock(Now);
        var store = new InMemoryConnectAuthorizationCodeStore(clock);
        return (new ConnectAuthorizationCodeService(store, clock), clock);
    }

    private static Task<string> IssueAsync(ConnectAuthorizationCodeService service) =>
        service.IssueAsync(UserId, Client, ReturnUri, Pkce.ChallengeFor(Verifier), Pkce.MethodS256);

    [Fact]
    public async Task Redeem_WithMatchingVerifier_ReturnsTheBoundUser()
    {
        var (service, _) = NewService();
        var code = await IssueAsync(service);

        var userId = await service.RedeemAsync(code, Verifier);

        Assert.Equal(UserId, userId);
    }

    [Fact]
    public async Task Redeem_IsSingleUse_TheSecondAttemptFails()
    {
        var (service, _) = NewService();
        var code = await IssueAsync(service);

        Assert.Equal(UserId, await service.RedeemAsync(code, Verifier));
        Assert.Null(await service.RedeemAsync(code, Verifier));
    }

    [Fact]
    public async Task Redeem_AfterExpiry_Fails()
    {
        var (service, clock) = NewService();
        var code = await IssueAsync(service);

        clock.Advance(ConnectAuthorizationCodeService.CodeLifetime + TimeSpan.FromSeconds(1));

        Assert.Null(await service.RedeemAsync(code, Verifier));
    }

    [Fact]
    public async Task Redeem_JustBeforeExpiry_StillSucceeds()
    {
        var (service, clock) = NewService();
        var code = await IssueAsync(service);

        clock.Advance(ConnectAuthorizationCodeService.CodeLifetime - TimeSpan.FromSeconds(1));

        Assert.Equal(UserId, await service.RedeemAsync(code, Verifier));
    }

    [Fact]
    public async Task Redeem_WithAWrongVerifier_Fails_AndConsumesTheCode()
    {
        var (service, _) = NewService();
        var code = await IssueAsync(service);

        Assert.Null(await service.RedeemAsync(code, "the-wrong-verifier"));

        // The code was consumed even though PKCE failed, so it cannot be brute-forced by re-presenting.
        Assert.Null(await service.RedeemAsync(code, Verifier));
    }

    [Fact]
    public async Task Redeem_AnUnknownCode_Fails()
    {
        var (service, _) = NewService();

        Assert.Null(await service.RedeemAsync("never-issued", Verifier));
    }

    [Fact]
    public async Task Issue_ReturnsAnOpaqueCode_NotThePlainChallengeOrUserId()
    {
        var (service, _) = NewService();
        var code = await IssueAsync(service);

        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.DoesNotContain(UserId.ToString(), code, StringComparison.OrdinalIgnoreCase);
    }
}
