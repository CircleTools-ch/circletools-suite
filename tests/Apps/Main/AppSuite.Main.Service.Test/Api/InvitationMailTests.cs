using System.Net;
using System.Net.Http.Json;
using AppSuite.Circles.Contracts;

namespace AppSuite.Main.Service.Test.Api;

/// <summary>
/// Covers the invitation mail as a <em>delivered message</em> — through the endpoint, the outbox and
/// real SMTP into Mailpit (FR-017, NFR-003, ADR-0023).
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class InvitationMailTests(MailApiFactory factory)
{
    /// <summary>
    /// The mail is written in the recipient's language when the address has an account, and in the
    /// inviter's otherwise. The inviter never sees the mail, so choosing by recipient leaks nothing
    /// back to them (FR-017).
    /// </summary>
    [Fact]
    public async Task Mail_UsesTheRecipientsAccountLanguage_OtherwiseTheInvitersUsingMailpit()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna", "de-CH");
        var claude = await CircleTestAccounts.SignUpAsync(factory, "Claude", "fr-CH");
        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");

        var stranger = $"stranger-{Guid.CreateVersion7():N}@example.test";

        await Invite(anna, circle.Id, claude.Email);
        await Invite(anna, circle.Id, stranger);
        await CircleTestAccounts.DispatchOutboxAsync(factory);

        var toClaude = await factory.WaitForMailAsync(claude.Email, "invite");
        var toStranger = await factory.WaitForMailAsync(stranger, "lädt");

        // Claude has an account and reads French; the stranger has none, so Anna's German is used.
        Assert.Contains("vous invite", toClaude.GetProperty("Subject").GetString()!, StringComparison.Ordinal);
        Assert.Contains("lädt Sie", toStranger.GetProperty("Subject").GetString()!, StringComparison.Ordinal);

        var strangerBody = await factory.ReadBodyAsync(toStranger.GetProperty("ID").GetString()!);
        var claudeBody = await factory.ReadBodyAsync(toClaude.GetProperty("ID").GetString()!);

        // Both mails name the circle and the inviter, and nobody else in it.
        Assert.Contains("Müller", strangerBody, StringComparison.Ordinal);
        Assert.Contains("Anna", strangerBody, StringComparison.Ordinal);

        // The address with no account gets a link to answer with; the one with an account is sent to
        // the app, because its accept path is bound to a verified address and needs no token.
        Assert.Contains("/invitation/", strangerBody, StringComparison.Ordinal);
        Assert.DoesNotContain("/invitation/", claudeBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whatever the address turns out to be, the inviter is told exactly one thing. This is the
    /// oracle ADR-0018 closed, and the endpoint is where it would reopen (NFR-005).
    /// </summary>
    [Fact]
    public async Task Invite_AnswersTheSameForAnAddressWithAnAccountAndOneWithout()
    {
        var anna = await CircleTestAccounts.SignUpAsync(factory, "Anna");
        var bea = await CircleTestAccounts.SignUpAsync(factory, "Bea");
        var circle = await CircleFixture.CreateAsync(anna, "Müller", "Familie");

        var withAnAccount = await Invite(anna, circle.Id, bea.Email);
        var withNone = await Invite(anna, circle.Id, $"nobody-{Guid.CreateVersion7():N}@example.test");

        Assert.Equal(HttpStatusCode.Accepted, withAnAccount.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, withNone.StatusCode);
        Assert.Equal(
            await withNone.Content.ReadAsStringAsync(),
            await withAnAccount.Content.ReadAsStringAsync());
    }

    private static async Task<HttpResponseMessage> Invite(CircleTestAccount inviter, Guid circleId, string email) =>
        await inviter.Client.PostAsJsonAsync(
            $"/api/v1/circles/{circleId}/invitations",
            new InviteRequest(email, ManagedMembershipId: null));
}
