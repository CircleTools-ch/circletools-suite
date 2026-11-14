// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.ApiClient;

/// <summary>
/// Persists the refresh token — the one credential the client is allowed to keep (ADR-0021,
/// ADR-0026).
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Reads the stored refresh token, from either scope.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The token, or <see langword="null"/> when there is no remembered session.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a refresh token, replacing whatever was there.</summary>
    /// <param name="refreshToken">The token to remember.</param>
    /// <param name="staySignedIn">
    /// <see langword="true"/> to survive a browser restart (<see cref="StorageScope.Durable"/>);
    /// <see langword="false"/> to last only for the browsing session.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the token is stored.</returns>
    ValueTask SaveAsync(string refreshToken, bool staySignedIn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a rotated refresh token, leaving it in the scope the previous one was in.
    /// </summary>
    /// <remarks>
    /// Every refresh rotates the token (ADR-0021), and a rotation is not a new decision about how
    /// long the session should last — writing it through <see cref="SaveAsync"/> would force the
    /// caller to remember a "stay signed in" answer given hours ago, and getting that wrong either
    /// silently promotes a session to permanent or drops one the user asked to keep.
    /// </remarks>
    /// <param name="refreshToken">The rotated token.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the token is stored.</returns>
    ValueTask RenewAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers which account the stored refresh token belongs to, in the scope the token is in.
    /// </summary>
    /// <remarks>
    /// The access token lives in memory only (ADR-0021), so a cold start without a reachable server knows
    /// nothing about the person until the refresh succeeds. The account id is not a credential; it only lets
    /// the app keep showing that person's data, kept on the device, until the session is renewed.
    /// </remarks>
    /// <param name="accountId">The account the session belongs to.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the id is stored.</returns>
    ValueTask RememberAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>The account the stored refresh token belongs to, if it was remembered.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account id, or <see langword="null"/> when there is no remembered session or no id.</returns>
    ValueTask<Guid?> ReadAccountAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes the refresh token, and the account it belongs to, from <b>both</b> scopes.</summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>A task that completes once no token remains anywhere.</returns>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
