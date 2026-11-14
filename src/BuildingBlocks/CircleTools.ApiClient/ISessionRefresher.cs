// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.ApiClient;

/// <summary>
/// Exchanges the stored refresh token for a new session.
/// </summary>
/// <remarks>
/// This seam exists to break a cycle: <see cref="AuthorizedHttpMessageHandler"/> needs to refresh,
/// but the refresh route belongs to an app's own API. The implementation lives with that app's
/// typed client and <b>must call the refresh endpoint on an unauthenticated
/// <see cref="HttpClient"/></b> — routing it back through the authorizing handler would recurse.
/// </remarks>
public interface ISessionRefresher
{
    /// <summary>Attempts to refresh the session.</summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>
    /// The new access token, or <see langword="null"/> if the session could not be renewed — in which
    /// case the caller is signed out and both token stores have been cleared.
    /// </returns>
    ValueTask<string?> RefreshAsync(CancellationToken cancellationToken = default);
}
