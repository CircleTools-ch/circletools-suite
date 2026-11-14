// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Coverage of <c>POST /resend-verification</c> (FR-007, NFR-005).
/// </summary>
/// <remarks>
/// Verification is the one step of sign-up a user cannot retry on their own — the mail can be lost,
/// filtered or deleted, and without this the account is stranded. What makes it delicate is that
/// "already verified" is exactly the fact an attacker wants, so the response must not vary.
/// </remarks>
[Collection(AccountApi.CollectionName)]
public sealed class ResendVerificationTests(AccountApiFactory factory)
{
    private const string ValidPassword = "correct-horse-battery";

    private static string UniqueEmail() => $"resend-{Guid.CreateVersion7():N}@example.test";

    private HttpClient Client() => factory.CreateClient();

    private async Task<List<OutboxMessage>> OutboxForAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var all = await context.OutboxMessages.AsNoTracking().ToListAsync();

        return [.. all.Where(m => m.Payload.Contains(email, StringComparison.OrdinalIgnoreCase))];
    }

    private async Task<string> RegisterAsync()
    {
        var email = UniqueEmail();
        var response = await Client().PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "de-CH"));

        response.EnsureSuccessStatusCode();
        return email;
    }

    private Task<HttpResponseMessage> ResendAsync(string email) =>
        Client().PostAsJsonAsync("/api/v1/account/resend-verification", new ResendVerificationRequest(email));

    [Fact]
    public async Task PendingAccount_GetsAFreshVerificationMail()
    {
        var email = await RegisterAsync();

        var response = await ResendAsync(email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var messages = await OutboxForAsync(email);
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(AccountEndpoints.VerificationMailType, m.Type));
    }

    [Fact]
    public async Task UnknownAddress_AnswersIdenticallyAndSendsNothing()
    {
        var unknown = UniqueEmail();

        var known = await ResendAsync(await RegisterAsync());
        var stranger = await ResendAsync(unknown);

        Assert.Equal(known.StatusCode, stranger.StatusCode);
        Assert.Equal(
            await known.Content.ReadAsStringAsync(),
            await stranger.Content.ReadAsStringAsync());
        Assert.Empty(await OutboxForAsync(unknown));
    }

    [Fact]
    public async Task AlreadyVerifiedAccount_AnswersIdenticallyAndSendsNothingFurther()
    {
        var email = await RegisterAsync();
        var queued = Assert.Single(await OutboxForAsync(email));
        var payload = JsonDocument.Parse(queued.Payload).RootElement;

        var verify = await Client().PostAsJsonAsync(
            "/api/v1/account/verify-email",
            new VerifyEmailRequest(
                payload.GetProperty("userId").GetString()!,
                payload.GetProperty("token").GetString()!));
        verify.EnsureSuccessStatusCode();

        var response = await ResendAsync(email);

        // Answering differently here would turn the endpoint into the enumeration oracle sign-up
        // itself carefully is not, and "this address is already verified" is the prize.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(await OutboxForAsync(email));
    }
}
