using AppSuite.Identity.Tokens;

namespace AppSuite.Identity.Test;

/// <summary>
/// Covers the refresh-rotation rows of TP-002 — the highest-risk logic in M1 (FR-008, ADR-0021).
/// Time is supplied explicitly, so the 30-day and grace-window boundaries are exercised without
/// any wall-clock waiting.
/// </summary>
public sealed class RefreshTokenChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("0198a1f0-0000-7000-8000-000000000001");

    private static TokenLifetimeOptions Options() => new();

    private static RefreshTokenChain StartedChain() =>
        RefreshTokenChain.Start(UserId, "h0", Now, Options());

    [Fact]
    public void Start_IssuesFirstTokenWithConfiguredLifetime()
    {
        var options = Options();

        var chain = RefreshTokenChain.Start(UserId, "h0", Now, options);

        Assert.Equal(UserId, chain.UserId);
        Assert.False(chain.IsRevoked);
        var token = Assert.Single(chain.Tokens);
        Assert.Equal("h0", token.Handle);
        Assert.Equal(Now + options.RefreshTokenLifetime, token.ExpiresAt);
        Assert.False(token.IsConsumed);
    }

    [Fact]
    public void Redeem_UnusedToken_RotatesAndInvalidatesPredecessor()
    {
        var chain = StartedChain();

        var result = chain.Redeem("h0", "h1", Now.AddMinutes(20), Options());

        var rotated = Assert.IsType<RefreshTokenRedemption.Rotated>(result);
        Assert.Equal("h1", rotated.Token.Handle);
        Assert.True(chain.Tokens[0].IsConsumed);
        Assert.Equal("h1", chain.Tokens[0].SuccessorHandle);
        Assert.Equal("h1", chain.Current.Handle);
    }

    [Fact]
    public void Redeem_SlidesTheWindow_OnEveryRotation()
    {
        var options = Options();
        var chain = StartedChain();
        var later = Now.AddDays(20);

        var result = chain.Redeem("h0", "h1", later, options);

        var rotated = Assert.IsType<RefreshTokenRedemption.Rotated>(result);
        Assert.Equal(later + options.RefreshTokenLifetime, rotated.Token.ExpiresAt);
    }

    [Fact]
    public void Redeem_ConsumedTokenReplayedOutsideGrace_IsTheft_AndRevokesWholeChain()
    {
        var options = Options();
        var chain = StartedChain();
        chain.Redeem("h0", "h1", Now, options);

        var replayAt = Now + options.RefreshReplayGraceWindow + TimeSpan.FromSeconds(1);
        var result = chain.Redeem("h0", "h2", replayAt, options);

        var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
        Assert.Equal(RefreshTokenRejection.ReuseDetected, rejected.Reason);
        Assert.True(chain.IsRevoked);
        Assert.Equal(replayAt, chain.RevokedAt);
    }

    [Fact]
    public void Redeem_AfterTheftDetected_EvenTheValidSuccessorIsRefused()
    {
        var options = Options();
        var chain = StartedChain();
        chain.Redeem("h0", "h1", Now, options);
        chain.Redeem("h0", "h2", Now.AddMinutes(5), options); // trips detection

        var result = chain.Redeem("h1", "h3", Now.AddMinutes(6), options);

        var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
        Assert.Equal(RefreshTokenRejection.ChainRevoked, rejected.Reason);
    }

    [Fact]
    public void Redeem_TwoConcurrentRefreshes_DoNotRevokeChain()
    {
        // ADR-0021's racing-refresh case: two tabs hold h0 and redeem it at almost the same
        // moment. Both must end up signed in, and the chain must survive.
        var options = Options();
        var chain = StartedChain();

        var first = chain.Redeem("h0", "h1", Now, options);
        var second = chain.Redeem("h0", "h2", Now.AddMilliseconds(80), options);

        var rotated = Assert.IsType<RefreshTokenRedemption.Rotated>(first);
        var replayed = Assert.IsType<RefreshTokenRedemption.ReplayedWithinGrace>(second);

        Assert.False(chain.IsRevoked);
        Assert.Equal("h1", rotated.Token.Handle);
        Assert.Equal("h1", replayed.Token.Handle); // same successor, not a second one
        Assert.Equal(2, chain.Tokens.Count); // "h2" was never issued
    }

    [Fact]
    public void Redeem_ReplayAtTheGraceBoundary_IsStillTreatedAsConcurrent()
    {
        var options = Options();
        var chain = StartedChain();
        chain.Redeem("h0", "h1", Now, options);

        var result = chain.Redeem("h0", "h2", Now + options.RefreshReplayGraceWindow, options);

        Assert.IsType<RefreshTokenRedemption.ReplayedWithinGrace>(result);
        Assert.False(chain.IsRevoked);
    }

    [Fact]
    public void Redeem_ExpiredToken_IsRejectedWithoutRevokingTheChain()
    {
        var options = Options();
        var chain = StartedChain();

        var result = chain.Redeem("h0", "h1", Now + options.RefreshTokenLifetime, options);

        var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
        Assert.Equal(RefreshTokenRejection.Expired, rejected.Reason);
        Assert.False(chain.IsRevoked);
    }

    [Fact]
    public void Redeem_UnknownHandle_IsRejected()
    {
        var chain = StartedChain();

        var result = chain.Redeem("nope", "h1", Now, Options());

        var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
        Assert.Equal(RefreshTokenRejection.UnknownToken, rejected.Reason);
        Assert.False(chain.IsRevoked);
    }

    [Fact]
    public void Redeem_OnRevokedChain_IsRejected()
    {
        var chain = StartedChain();
        chain.Revoke(Now);

        var result = chain.Redeem("h0", "h1", Now.AddMinutes(1), Options());

        var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
        Assert.Equal(RefreshTokenRejection.ChainRevoked, rejected.Reason);
    }

    [Fact]
    public void Revoke_KeepsTheFirstRevocationTime()
    {
        var chain = StartedChain();

        chain.Revoke(Now);
        chain.Revoke(Now.AddHours(1));

        Assert.Equal(Now, chain.RevokedAt);
    }

    [Fact]
    public void Redeem_LongChain_RotatesRepeatedlyWithoutFalsePositives()
    {
        var options = Options();
        var chain = StartedChain();
        var at = Now;

        for (var i = 1; i <= 20; i++)
        {
            at = at.AddMinutes(14);
            var result = chain.Redeem($"h{i - 1}", $"h{i}", at, options);
            Assert.IsType<RefreshTokenRedemption.Rotated>(result);
        }

        Assert.False(chain.IsRevoked);
        Assert.Equal("h20", chain.Current.Handle);
        Assert.Equal(21, chain.Tokens.Count);
    }
}
