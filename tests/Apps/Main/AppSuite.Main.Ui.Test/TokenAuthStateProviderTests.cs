using AppSuite.ApiClient;
using AppSuite.Main.Ui.Test.TestSupport;
using AppSuite.Ui.Account.Auth;

namespace AppSuite.Main.Ui.Test;

/// <summary>
/// Tests for the principal the UI is built from (ADR-0021). The access token is the only thing the
/// client knows about the user, so anything it cannot read must come out as signed out.
/// </summary>
public sealed class TokenAuthStateProviderTests
{
    [Fact]
    public async Task WithoutAToken_TheUserIsAnonymous()
    {
        var provider = new TokenAuthStateProvider(new InMemoryAccessTokenStore(TimeProvider.System));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task WithAToken_TheClaimsAreReadableByTheUi()
    {
        var userId = Guid.CreateVersion7();
        var tokens = new InMemoryAccessTokenStore(TimeProvider.System);
        tokens.SetToken(
            AccountTestContext.Jwt(userId, "Anna", "anna@example.test", emailVerified: true, locale: "fr-CH"),
            TimeSpan.FromMinutes(15));

        var state = await new TokenAuthStateProvider(tokens).GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(userId, state.User.UserId());
        Assert.Equal("Anna", state.User.DisplayName());
        Assert.Equal("anna@example.test", state.User.Email());
        Assert.True(state.User.IsEmailVerified());
        Assert.Equal("fr-CH", state.User.Locale());
    }

    [Fact]
    public async Task PendingAccount_IsSignedInButNotVerified()
    {
        var tokens = new InMemoryAccessTokenStore(TimeProvider.System);
        tokens.SetToken(
            AccountTestContext.Jwt(Guid.CreateVersion7(), emailVerified: false),
            TimeSpan.FromMinutes(15));

        var state = await new TokenAuthStateProvider(tokens).GetAuthenticationStateAsync();

        // FR-007: a pending account may sign in, but nothing account-scoped may happen yet.
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.False(state.User.IsEmailVerified());
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("only.two")]
    [InlineData("header.!!!not-base64!!!.signature")]
    public async Task UnreadableToken_IsTreatedAsSignedOut(string token)
    {
        var tokens = new InMemoryAccessTokenStore(TimeProvider.System);
        tokens.SetToken(token, TimeSpan.FromMinutes(15));

        var state = await new TokenAuthStateProvider(tokens).GetAuthenticationStateAsync();

        // The only safe reading of a token we cannot understand. Trusting it would build a UI on a
        // claim nobody made.
        Assert.False(state.User.Identity?.IsAuthenticated);
    }
}
