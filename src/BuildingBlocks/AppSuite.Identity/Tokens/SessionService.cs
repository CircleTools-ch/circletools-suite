using Microsoft.Extensions.Options;

namespace AppSuite.Identity.Tokens;

/// <summary>
/// Starts, refreshes and ends sign-in sessions — the single place the access token and the
/// refresh chain are kept in step (FR-008, FR-009, FR-015).
/// </summary>
/// <param name="chains">Refresh-chain persistence.</param>
/// <param name="users">User lookup for minting tokens on refresh.</param>
/// <param name="issuer">Access-token issuer.</param>
/// <param name="lifetimes">Configured lifetimes and the replay grace window.</param>
/// <param name="clock">Time source, injected so expiry and grace are testable.</param>
public sealed class SessionService(
    IRefreshTokenChainStore chains,
    ISessionUserLookup users,
    AccessTokenIssuer issuer,
    IOptions<TokenLifetimeOptions> lifetimes,
    TimeProvider clock)
{
    /// <summary>Begins a session after a successful sign-in.</summary>
    /// <param name="user">The user signing in.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token and the refresh secret to hand to the client.</returns>
    public async Task<SessionTokens> StartAsync(AppSuiteUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = clock.GetUtcNow();
        var options = lifetimes.Value;
        var (secret, handle) = RefreshTokenSecret.Create();

        var chain = RefreshTokenChain.Start(user.Id, handle, now, options);
        await chains.AddAsync(chain, cancellationToken).ConfigureAwait(false);
        await chains.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var access = issuer.Issue(user);
        return new SessionTokens(access.Value, access.ExpiresAt, secret, chain.Current.ExpiresAt);
    }

    /// <summary>Exchanges a refresh secret for a new token pair.</summary>
    /// <param name="presentedSecret">The refresh secret the client presented.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new tokens, or why the refresh was refused.</returns>
    /// <remarks>
    /// Changes are saved on <em>every</em> path, including rejection — detecting reuse revokes the
    /// chain, and that revocation is the security outcome. Returning early without saving would
    /// leave a known-compromised session live.
    /// </remarks>
    public async Task<SessionRefreshResult> RefreshAsync(
        string presentedSecret,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presentedSecret);

        var presentedHandle = RefreshTokenSecret.HandleFor(presentedSecret);
        var chain = await chains.FindByTokenHandleAsync(presentedHandle, cancellationToken).ConfigureAwait(false);

        if (chain is null)
        {
            return new SessionRefreshResult.Failed(RefreshTokenRejection.UnknownToken);
        }

        var now = clock.GetUtcNow();
        var options = lifetimes.Value;
        var (newSecret, newHandle) = RefreshTokenSecret.Create();
        var redemption = chain.Redeem(presentedHandle, newHandle, newSecret, now, options);

        if (redemption is RefreshTokenRedemption.Rejected rejected)
        {
            await chains.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SessionRefreshResult.Failed(rejected.Reason);
        }

        var user = await users.FindByIdAsync(chain.UserId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            // The account was deleted while the session was live (FR-015). Nothing to sign, and
            // the chain should not outlive its owner.
            chain.Revoke(now);
            await chains.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new SessionRefreshResult.Failed(RefreshTokenRejection.ChainRevoked);
        }

        // A concurrent request is answered with the token it already produced, not a new one.
        var (refreshSecret, refreshToken) = redemption switch
        {
            RefreshTokenRedemption.Rotated rotated => (newSecret, rotated.Token),
            RefreshTokenRedemption.ReplayedWithinGrace replayed => (replayed.Secret, replayed.Token),
            _ => throw new InvalidOperationException($"Unhandled redemption {redemption.GetType().Name}."),
        };

        await chains.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var access = issuer.Issue(user);
        return new SessionRefreshResult.Succeeded(
            new SessionTokens(access.Value, access.ExpiresAt, refreshSecret, refreshToken.ExpiresAt));
    }

    /// <summary>
    /// Ends every session a user has. Called by password reset (FR-009) and account deletion
    /// (FR-015), both of which must eject an intruder rather than merely inconvenience them.
    /// </summary>
    /// <param name="userId">The user whose sessions end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many sessions were revoked.</returns>
    public async Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var revoked = await chains
            .RevokeAllForUserAsync(userId, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        await chains.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return revoked;
    }
}
