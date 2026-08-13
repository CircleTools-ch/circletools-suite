using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// Proves the ADR-0023 seam end to end: registering queues a message, the dispatcher renders and
/// sends it over SMTP, and it arrives in the Mailpit sink as a real message.
/// </summary>
/// <remarks>
/// Asserting on a delivered message rather than a mocked sender is the whole point of running a
/// mail container in development — it catches a broken template, a malformed address or an
/// unreachable transport, none of which a mock can.
/// </remarks>
[Collection(MailApi.CollectionName)]
public sealed class MailDeliveryTests(MailApiFactory factory)
{
    private const string ValidPassword = "correct-horse-battery";

    private static string UniqueEmail() => $"mail-{Guid.CreateVersion7():N}@example.test";

    [Theory]
    [InlineData("de-CH", "Bestätige deine E-Mail-Adresse")]
    [InlineData("fr-CH", "Confirmez votre adresse e-mail")]
    [InlineData("en", "Confirm your email address")]
    public async Task Registering_DeliversTheVerificationMailInTheChosenLanguage(string language, string subject)
    {
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, language));
        response.EnsureSuccessStatusCode();

        await DispatchOutboxAsync();

        var message = await factory.WaitForMailAsync(email);

        Assert.Equal(subject, message.GetProperty("Subject").GetString());
        Assert.Equal(
            "no-reply@circletools.test",
            message.GetProperty("From").GetProperty("Address").GetString());
    }

    [Fact]
    public async Task VerificationMail_CarriesALinkThatActuallyVerifies()
    {
        // The strongest check available: take the link out of the delivered mail, use it, and
        // confirm the account becomes verified. A template that renders but encodes the token
        // wrongly would pass every unit test and fail here.
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "en"));
        await DispatchOutboxAsync();

        var message = await factory.WaitForMailAsync(email);
        var body = await factory.ReadBodyAsync(message.GetProperty("ID").GetString()!);

        var (userId, token) = ParseVerificationLink(body);

        var verify = await client.PostAsJsonAsync(
            "/api/v1/account/verify-email",
            new VerifyEmailRequest(userId, token));
        verify.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync(
            "/api/v1/account/login",
            new LoginRequest(email, ValidPassword));
        login.EnsureSuccessStatusCode();

        var session = await login.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.True(session.EmailVerified);
    }

    [Fact]
    public async Task Dispatcher_MarksMessagesProcessed_SoTheyAreNotSentTwice()
    {
        var email = UniqueEmail();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/account/register",
            new RegisterRequest("Anna", email, ValidPassword, "en"));

        var firstPass = await DispatchOutboxAsync();
        var secondPass = await DispatchOutboxAsync();

        Assert.True(firstPass >= 1, "the first pass should send the queued mail");
        Assert.Equal(0, secondPass);
    }

    private async Task<int> DispatchOutboxAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        return await dispatcher.DispatchPendingAsync(batchSize: 50, maxAttempts: 5);
    }

    private static (string UserId, string Token) ParseVerificationLink(string body)
    {
        var start = body.IndexOf("http", StringComparison.Ordinal);
        Assert.True(start >= 0, $"no link found in body:\n{body}");

        var end = body.IndexOfAny(['\n', '\r'], start);
        var link = end < 0 ? body[start..] : body[start..end];
        var query = new Uri(link.Trim()).Query.TrimStart('?');
        var parts = query.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.Ordinal);

        return (parts["userId"], parts["token"]);
    }
}
