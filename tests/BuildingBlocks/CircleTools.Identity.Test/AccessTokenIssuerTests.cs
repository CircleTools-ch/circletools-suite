using CircleTools.Identity.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CircleTools.Identity.Test;

/// <summary>Covers access-token issuance (FR-008, ADR-0021).</summary>
public sealed class AccessTokenIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_ExpiresAfterTheConfiguredAccessTokenLifetime()
    {
        var clock = new TestClock(Now);
        var issuer = TestFactory.Issuer(clock);

        var token = issuer.Issue(TestFactory.User());

        Assert.Equal(Now + new TokenLifetimeOptions().AccessTokenLifetime, token.ExpiresAt);
    }

    [Fact]
    public void Issue_CarriesTheClaimsEndpointsAuthoriseOn()
    {
        var clock = new TestClock(Now);
        var user = TestFactory.User();

        var token = TestFactory.Issuer(clock).Issue(user);
        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        Assert.Equal(user.Id.ToString(), parsed.GetClaim(JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, parsed.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Anna", parsed.GetClaim(JwtRegisteredClaimNames.Name).Value);
        Assert.Equal("de-CH", parsed.GetClaim(AccessTokenIssuer.LocaleClaim).Value);
    }

    [Fact]
    public void Issue_MarksAPendingAccountAsUnverified()
    {
        // FR-007: a pending account may sign in, so the token must carry enough for endpoints to
        // refuse the account-scoped parts. Getting this backwards silently opens them up.
        var clock = new TestClock(Now);

        var pending = TestFactory.Issuer(clock).Issue(TestFactory.User(emailConfirmed: false));
        var verified = TestFactory.Issuer(clock).Issue(TestFactory.User(emailConfirmed: true));

        var handler = new JsonWebTokenHandler();
        Assert.Equal("False", handler.ReadJsonWebToken(pending.Value)
            .GetClaim(AccessTokenIssuer.EmailVerifiedClaim).Value, ignoreCase: true);
        Assert.Equal("True", handler.ReadJsonWebToken(verified.Value)
            .GetClaim(AccessTokenIssuer.EmailVerifiedClaim).Value, ignoreCase: true);
    }

    [Fact]
    public void Issue_GivesEveryTokenADistinctIdentifier()
    {
        var clock = new TestClock(Now);
        var issuer = TestFactory.Issuer(clock);
        var user = TestFactory.User();

        var handler = new JsonWebTokenHandler();
        var first = handler.ReadJsonWebToken(issuer.Issue(user).Value);
        var second = handler.ReadJsonWebToken(issuer.Issue(user).Value);

        Assert.NotEqual(
            first.GetClaim(JwtRegisteredClaimNames.Jti).Value,
            second.GetClaim(JwtRegisteredClaimNames.Jti).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void Issue_RefusesToSignWithAWeakKey(string key)
    {
        // A forgeable signature in a bearer model means minting sessions for any account, so this
        // must fail loudly rather than produce a token nobody notices is weak.
        var issuer = TestFactory.Issuer(new TestClock(Now), key);

        var exception = Assert.Throws<InvalidOperationException>(() => issuer.Issue(TestFactory.User()));

        Assert.Contains("SigningKey", exception.Message, StringComparison.Ordinal);
    }
}
