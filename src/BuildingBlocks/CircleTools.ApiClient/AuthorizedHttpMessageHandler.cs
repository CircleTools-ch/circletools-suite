// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;

namespace CircleTools.ApiClient;

/// <summary>
/// Attaches the bearer token to outgoing requests and renews it when the server says it is stale.
/// </summary>
/// <remarks>
/// <para>
/// Two refresh triggers, for different reasons. <b>Proactively</b>, when the token is within
/// <see cref="ExpiryMargin"/> of expiring — a token that dies mid-flight costs a wasted round trip.
/// <b>Reactively</b>, on a <c>401</c> — because the server may have revoked the session (a password
/// reset revokes every session) while the token still looks valid to us.
/// </para>
/// <para>
/// A single request is retried at most once. If the refresh itself fails, the original <c>401</c> is
/// returned unchanged and the caller signs out; retrying a second time would only turn one rejection
/// into a loop.
/// </para>
/// </remarks>
public sealed class AuthorizedHttpMessageHandler : DelegatingHandler
{
    /// <summary>How close to expiry a token is refreshed before it is used.</summary>
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

    private readonly IAccessTokenStore _tokens;
    private readonly ISessionRefresher _refresher;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>Initialises the handler.</summary>
    /// <param name="tokens">The in-memory access token store.</param>
    /// <param name="refresher">The app's refresh implementation.</param>
    public AuthorizedHttpMessageHandler(IAccessTokenStore tokens, ISessionRefresher refresher)
    {
        _tokens = tokens;
        _refresher = refresher;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The refresh call itself, and anonymous calls like sign-in, opt out entirely.
        if (request.Options.TryGetValue(ApiRequestOptions.SkipAuthorization, out var skip) && skip)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var token = _tokens.AccessToken;
        if (token is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        if (_tokens.NeedsRefresh(ExpiryMargin))
        {
            token = await RefreshAsync(token, cancellationToken);
            if (token is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }
        }

        // Buffer now, while the content is still readable: a 401 retry has to send the same body,
        // and by then the original stream has been consumed.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        Authorize(request, token);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refreshed = await RefreshAsync(token, cancellationToken);
        if (refreshed is null)
        {
            return response;
        }

        response.Dispose();

        using var retry = await CloneAsync(request, cancellationToken);
        Authorize(retry, refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshLock.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void Authorize(HttpRequestMessage request, string token) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>
    /// Refreshes the session, collapsing concurrent attempts into one.
    /// </summary>
    /// <remarks>
    /// Several requests can fail with <c>401</c> at the same moment — a dashboard firing three calls
    /// on load, or two open tabs. Each must not present the same refresh token: the server treats a
    /// replayed refresh token as theft and revokes the whole chain (ADR-0021). It does forgive
    /// replays inside a 30-second grace window, but relying on that as normal operation would be
    /// building on the safety net. So the first caller through the lock refreshes, and the rest
    /// notice the token already changed and reuse it.
    /// </remarks>
    /// <param name="staleToken">The token the caller found unusable.</param>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    /// <returns>A usable access token, or <see langword="null"/> if the session is over.</returns>
    private async Task<string?> RefreshAsync(string staleToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = _tokens.AccessToken;
            if (current is not null && !string.Equals(current, staleToken, StringComparison.Ordinal))
            {
                return current;
            }

            return await _refresher.RefreshAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Options carry the cross-origin credentials marker the passkey ceremonies depend on;
        // a retry that dropped it would fail for a completely unrelated-looking reason.
        foreach (var option in request.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var content = new ByteArrayContent(bytes);

            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }
}
