// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using System.Net.Http.Json;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>A signed-in, verified account and the client that carries its bearer token.</summary>
/// <param name="Email">The account's verified address.</param>
/// <param name="DisplayName">The name others see.</param>
/// <param name="Client">A client already carrying the access token.</param>
public sealed record CircleTestAccount(string Email, string DisplayName, HttpClient Client);

/// <summary>
/// Signs accounts up through the real endpoints for the circles tests.
/// </summary>
/// <remarks>
/// Every circle test needs at least two identities — "which user is this?" is the thing under test —
/// so the caller is an explicit parameter everywhere rather than an ambient default.
/// </remarks>
internal static class CircleTestAccounts
{
    internal const string Password = "correct-horse-battery";

    /// <summary>Registers, verifies and signs in a fresh account.</summary>
    /// <param name="factory">The API fixture.</param>
    /// <param name="displayName">The name others will see.</param>
    /// <param name="language">The account's preferred language.</param>
    /// <returns>The account and its authenticated client.</returns>
    public static async Task<CircleTestAccount> SignUpAsync(
        MailApiFactory factory,
        string displayName,
        string language = "de-CH")
    {
        var email = $"circles-{Guid.CreateVersion7():N}@example.test";
        var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest(displayName, email, Password, language));

        await DispatchOutboxAsync(factory);

        var mail = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(mail.GetProperty("ID").GetString()!);
        var (userId, token) = LinkParser.Parse(body);

        await client.PostAsJsonAsync("/api/v1/account/verify-email", new VerifyEmailRequest(userId, token));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return new CircleTestAccount(email, displayName, client);
    }

    /// <summary>
    /// Registers and signs in an account whose address is never verified — a session, and nothing
    /// account-scoped it may do.
    /// </summary>
    /// <param name="factory">The API fixture.</param>
    /// <param name="client">The client to sign in on.</param>
    /// <param name="displayName">The name others would see.</param>
    /// <returns>The bearer header for the unverified session.</returns>
    public static async Task<AuthenticationHeaderValue> SignUpUnverifiedAsync(
        MailApiFactory factory,
        HttpClient client,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(client);

        var email = $"unverified-{Guid.CreateVersion7():N}@example.test";

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest(displayName, email, Password, "de-CH"));

        var login = await client.PostAsJsonAsync("/api/v1/account/login", new LoginRequest(email, Password));
        var session = (await login.Content.ReadFromJsonAsync<SessionResponse>())!;

        Assert.False(session.EmailVerified);

        return new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    /// <summary>Runs the outbox, so queued mail actually reaches the sink.</summary>
    /// <param name="factory">The API fixture.</param>
    /// <returns>How many messages were dispatched.</returns>
    public static async Task<int> DispatchOutboxAsync(MailApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider
            .GetRequiredService<OutboxDispatcher>()
            .DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
    }
}
