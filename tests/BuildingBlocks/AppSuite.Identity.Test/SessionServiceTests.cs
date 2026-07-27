using AppSuite.Identity.Tokens;

namespace AppSuite.Identity.Test;

/// <summary>
/// Covers the session lifecycle rows of TP-002 — the seam where the access token and the refresh
/// chain must stay in step (FR-008, FR-009, FR-015).
/// </summary>
public sealed class SessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static (SessionService Service, FakeChainStore Chains, FakeUserLookup Users, TestClock Clock)
        Build(bool emailConfirmed = true, AppSuiteUser? existing = null)
    {
        var clock = new TestClock(Now);
        var chains = new FakeChainStore();
        var users = new FakeUserLookup();
        var user = existing ?? TestFactory.User(emailConfirmed);
        users.Add(user);

        return (TestFactory.Session(chains, users, clock), chains, users, clock);
    }

    [Fact]
    public async Task Start_IssuesBothTokensAndPersistsTheChain()
    {
        var user = TestFactory.User();
        var (service, chains, _, _) = Build(existing: user);

        var tokens = await service.StartAsync(user);

        Assert.NotEmpty(tokens.AccessToken);
        Assert.NotEmpty(tokens.RefreshSecret);
        Assert.Equal(Now + new TokenLifetimeOptions().AccessTokenLifetime, tokens.AccessTokenExpiresAt);
        Assert.Equal(Now + new TokenLifetimeOptions().RefreshTokenLifetime, tokens.RefreshTokenExpiresAt);
        Assert.Single(chains.Chains);
    }

    [Fact]
    public async Task Start_StoresOnlyTheHashOfTheRefreshSecret()
    {
        var user = TestFactory.User();
        var (service, chains, _, _) = Build(existing: user);

        var tokens = await service.StartAsync(user);

        var stored = Assert.Single(chains.Chains).Current.Handle;
        Assert.NotEqual(tokens.RefreshSecret, stored);
        Assert.Equal(RefreshTokenSecret.HandleFor(tokens.RefreshSecret), stored);
    }

    [Fact]
    public async Task Refresh_RotatesAndInvalidatesThePresentedSecret()
    {
        var user = TestFactory.User();
        var (service, _, _, clock) = Build(existing: user);
        var started = await service.StartAsync(user);

        clock.Advance(TimeSpan.FromMinutes(14));
        var refreshed = await service.RefreshAsync(started.RefreshSecret);

        var succeeded = Assert.IsType<SessionRefreshResult.Succeeded>(refreshed);
        Assert.NotEqual(started.RefreshSecret, succeeded.Tokens.RefreshSecret);

        // The old secret is now spent; replaying it far later is theft.
        clock.Advance(TimeSpan.FromMinutes(5));
        var replayed = await service.RefreshAsync(started.RefreshSecret);

        var failed = Assert.IsType<SessionRefreshResult.Failed>(replayed);
        Assert.Equal(RefreshTokenRejection.ReuseDetected, failed.Reason);
    }

    [Fact]
    public async Task Refresh_ConcurrentCallers_BothReceiveTheSameUsableSecret()
    {
        var user = TestFactory.User();
        var (service, _, _, clock) = Build(existing: user);
        var started = await service.StartAsync(user);

        var first = await service.RefreshAsync(started.RefreshSecret);
        clock.Advance(TimeSpan.FromSeconds(2));
        var second = await service.RefreshAsync(started.RefreshSecret);

        var a = Assert.IsType<SessionRefreshResult.Succeeded>(first);
        var b = Assert.IsType<SessionRefreshResult.Succeeded>(second);

        Assert.Equal(a.Tokens.RefreshSecret, b.Tokens.RefreshSecret);

        // ...and that shared secret still works afterwards.
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.IsType<SessionRefreshResult.Succeeded>(await service.RefreshAsync(b.Tokens.RefreshSecret));
    }

    [Fact]
    public async Task Refresh_PersistsRevocationWhenReuseIsDetected()
    {
        // The security outcome of detection is the revocation. If it were not saved, a
        // known-compromised session would stay live.
        var user = TestFactory.User();
        var (service, chains, _, clock) = Build(existing: user);
        var started = await service.StartAsync(user);

        await service.RefreshAsync(started.RefreshSecret);
        clock.Advance(TimeSpan.FromMinutes(5));
        var savesBefore = chains.SaveCount;

        await service.RefreshAsync(started.RefreshSecret);

        Assert.True(chains.SaveCount > savesBefore, "revocation must be saved");
        Assert.True(Assert.Single(chains.Chains).IsRevoked);
    }

    [Fact]
    public async Task Refresh_UnknownSecret_IsRefused()
    {
        var (service, _, _, _) = Build();

        var result = await service.RefreshAsync(RefreshTokenSecret.Create().Secret);

        var failed = Assert.IsType<SessionRefreshResult.Failed>(result);
        Assert.Equal(RefreshTokenRejection.UnknownToken, failed.Reason);
    }

    [Fact]
    public async Task Refresh_AfterTheAccountIsDeleted_RevokesRatherThanIssuing()
    {
        // FR-015 deletes the account; a session that outlived it must not keep minting tokens.
        var user = TestFactory.User();
        var (service, chains, users, clock) = Build(existing: user);
        var started = await service.StartAsync(user);

        users.Remove(user.Id);
        clock.Advance(TimeSpan.FromMinutes(14));

        var result = await service.RefreshAsync(started.RefreshSecret);

        var failed = Assert.IsType<SessionRefreshResult.Failed>(result);
        Assert.Equal(RefreshTokenRejection.ChainRevoked, failed.Reason);
        Assert.True(Assert.Single(chains.Chains).IsRevoked);
    }

    [Fact]
    public async Task Refresh_ExpiredRefreshToken_IsRefusedWithoutRevokingTheChain()
    {
        var user = TestFactory.User();
        var (service, chains, _, clock) = Build(existing: user);
        var started = await service.StartAsync(user);

        clock.Advance(new TokenLifetimeOptions().RefreshTokenLifetime);
        var result = await service.RefreshAsync(started.RefreshSecret);

        var failed = Assert.IsType<SessionRefreshResult.Failed>(result);
        Assert.Equal(RefreshTokenRejection.Expired, failed.Reason);
        Assert.False(Assert.Single(chains.Chains).IsRevoked);
    }

    [Fact]
    public async Task RevokeAll_EndsEverySessionForOneUserOnly()
    {
        var user = TestFactory.User();
        var (service, chains, users, _) = Build(existing: user);
        var other = TestFactory.User();
        users.Add(other);

        var first = await service.StartAsync(user);
        await service.StartAsync(user);
        var untouched = await service.StartAsync(other);

        var revoked = await service.RevokeAllAsync(user.Id);

        Assert.Equal(2, revoked);
        Assert.Equal(2, chains.Chains.Count(c => c.UserId == user.Id && c.IsRevoked));

        var refused = await service.RefreshAsync(first.RefreshSecret);
        Assert.IsType<SessionRefreshResult.Failed>(refused);

        // The bystander's session is unaffected.
        Assert.IsType<SessionRefreshResult.Succeeded>(await service.RefreshAsync(untouched.RefreshSecret));
    }
}
