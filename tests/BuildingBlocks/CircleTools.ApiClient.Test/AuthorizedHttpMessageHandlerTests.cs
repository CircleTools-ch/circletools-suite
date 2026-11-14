// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using CircleTools.ApiClient.Test.TestSupport;

namespace CircleTools.ApiClient.Test;

/// <summary>
/// Tests for the handler that keeps a bearer session alive (ADR-0021).
/// </summary>
/// <remarks>
/// The behaviour worth pinning is not "it adds a header" but what happens when a token goes stale
/// mid-session: exactly one refresh, exactly one retry, the request body intact, and a clean stop
/// when the session is genuinely over. Getting the "exactly one" wrong is what makes the server
/// treat a legitimate client as a token thief.
/// </remarks>
public sealed class AuthorizedHttpMessageHandlerTests
{
    private static readonly Uri BaseAddress = new("https://api.test/");

    [Fact]
    public async Task Request_WithoutSession_GoesOutUnauthenticated()
    {
        var tokens = NewTokenStore();
        var wire = new TokenAwareHandler(validToken: null);

        using var client = Client(wire, tokens, new CountingRefresher(tokens));
        using var response = await client.GetAsync(new Uri("profile", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.Single(wire.Requests).BearerToken);
    }

    [Fact]
    public async Task Unauthorized_RefreshesOnceAndRetriesWithTheNewToken()
    {
        var tokens = NewTokenStore();
        tokens.SetToken("stale", TimeSpan.FromMinutes(15));

        var wire = new TokenAwareHandler("fresh");
        var refresher = new CountingRefresher(tokens, "fresh");

        using var client = Client(wire, tokens, refresher);
        using var response = await client.PostAsync(
            new Uri("profile", UriKind.Relative),
            new StringContent("{\"displayName\":\"Anna\"}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, refresher.Calls);
        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal("stale", wire.Requests[0].BearerToken);
        Assert.Equal("fresh", wire.Requests[1].BearerToken);
    }

    [Fact]
    public async Task Retry_ResendsTheOriginalBodyAndItsRequestOptions()
    {
        var tokens = NewTokenStore();
        tokens.SetToken("stale", TimeSpan.FromMinutes(15));

        var wire = new TokenAwareHandler("fresh");

        using var client = Client(wire, tokens, new CountingRefresher(tokens, "fresh"));
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri("passkeys", UriKind.Relative))
        {
            Content = new StringContent("{\"credentialJson\":\"...\"}"),
        };

        // The WebAuthn ceremonies ride on this marker. A retry that dropped it would fail as a
        // missing challenge, which looks nothing like an expired token.
        message.Options.Set(ApiRequestOptions.IncludeCredentials, true);

        using var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, wire.Requests.Count);
        Assert.Equal(wire.Requests[0].Body, wire.Requests[1].Body);
        Assert.True(wire.Requests[1].IncludeCredentials);
    }

    [Fact]
    public async Task ConcurrentUnauthorized_TriggerExactlyOneRefresh()
    {
        var tokens = NewTokenStore();
        tokens.SetToken("stale", TimeSpan.FromMinutes(15));

        var wire = new TokenAwareHandler("fresh");
        var refresher = new CountingRefresher(tokens, "fresh");

        using var client = Client(wire, tokens, refresher);

        var responses = await Task.WhenAll(Enumerable
            .Range(0, 3)
            .Select(_ => client.GetAsync(new Uri("profile", UriKind.Relative))));

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            response.Dispose();
        }

        // Presenting the same refresh token twice reads as theft to the server and revokes the
        // whole chain (ADR-0021). The 30-second grace window forgives it; relying on that as normal
        // operation would be building on the safety net.
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task TokenCloseToExpiry_IsRefreshedBeforeTheRequestGoesOut()
    {
        var tokens = NewTokenStore();

        // Inside the handler's 30-second margin, so it is refreshed rather than spent on a request
        // that would expire in flight.
        tokens.SetToken("expiring", TimeSpan.FromSeconds(10));

        var wire = new TokenAwareHandler("fresh");
        var refresher = new CountingRefresher(tokens, "fresh");

        using var client = Client(wire, tokens, refresher);
        using var response = await client.GetAsync(new Uri("profile", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, refresher.Calls);
        Assert.Equal("fresh", Assert.Single(wire.Requests).BearerToken);
    }

    [Fact]
    public async Task FailedRefresh_ReturnsTheUnauthorizedResponseWithoutRetrying()
    {
        var tokens = NewTokenStore();
        tokens.SetToken("stale", TimeSpan.FromMinutes(15));

        var wire = new TokenAwareHandler("fresh");
        var refresher = new CountingRefresher(tokens, result: null);

        using var client = Client(wire, tokens, refresher);
        using var response = await client.GetAsync(new Uri("profile", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(wire.Requests);
        Assert.Null(tokens.AccessToken);
    }

    [Fact]
    public async Task SkipAuthorization_BypassesTheHandlerEntirely()
    {
        var tokens = NewTokenStore();
        tokens.SetToken("live", TimeSpan.FromMinutes(15));

        var wire = new TokenAwareHandler("fresh");
        var refresher = new CountingRefresher(tokens, "fresh");

        using var client = Client(wire, tokens, refresher);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri("refresh", UriKind.Relative));
        message.Options.Set(ApiRequestOptions.SkipAuthorization, true);

        using var response = await client.SendAsync(message);

        // The refresh call must not be authorized, and above all must not trigger a refresh of its
        // own - that is the loop this flag exists to prevent.
        Assert.Equal(0, refresher.Calls);
        Assert.Null(Assert.Single(wire.Requests).BearerToken);
    }

    private static InMemoryAccessTokenStore NewTokenStore() => new(TimeProvider.System);

    private static HttpClient Client(
        HttpMessageHandler wire,
        IAccessTokenStore tokens,
        ISessionRefresher refresher)
    {
        var handler = new AuthorizedHttpMessageHandler(tokens, refresher) { InnerHandler = wire };
        return new HttpClient(handler) { BaseAddress = BaseAddress };
    }
}
