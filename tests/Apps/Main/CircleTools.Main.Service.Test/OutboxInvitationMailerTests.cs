using System.Text.Json;
using CircleTools.Circles.Core.Domain;
using CircleTools.Infrastructure.Mail;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Service;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Covers the host's <see cref="IInvitationMailer"/> adapter (FR-017): it must enqueue rather than
/// send, and what it enqueues must be what the template can render.
/// </summary>
public sealed class OutboxInvitationMailerTests
{
    private static readonly InvitationMail Invitation = new(
        "bea@example.test",
        Language: "fr-CH",
        CircleName: "Müller",
        CircleLabel: "Familie",
        InviterDisplayName: "Anna",
        Secret: "s3cr3t-link-value",
        RecipientHasAccount: false);

    [Fact]
    public async Task QueueAsync_EnqueuesOneInvitationMessage()
    {
        var outbox = new InMemoryOutbox();

        await new OutboxInvitationMailer(outbox, TimeProvider.System).QueueAsync(Invitation);

        var message = Assert.Single(outbox.Messages);
        Assert.Equal(MailTemplateRenderer.InvitationType, message.Type);
        Assert.True(message.IsPending);
    }

    /// <summary>
    /// The adapter and the template agree on nothing but a JSON shape, and a rename on either side
    /// would otherwise surface as an invitation that is silently never delivered.
    /// </summary>
    [Fact]
    public async Task WhatIsQueued_IsWhatTheTemplateCanRender()
    {
        var outbox = new InMemoryOutbox();
        var renderer = new MailTemplateRenderer(
            Options.Create(new MailOptions { ClientBaseUrl = "https://app.example.test/" }));

        await new OutboxInvitationMailer(outbox, TimeProvider.System).QueueAsync(Invitation);

        var message = Assert.Single(outbox.Messages);
        var mail = renderer.Render(message.Type, message.Payload);

        Assert.NotNull(mail);
        Assert.Equal("bea@example.test", mail.To);
        Assert.Contains("vous invite", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("Müller", mail.Body, StringComparison.Ordinal);
        Assert.Contains(
            "https://app.example.test/invitation/s3cr3t-link-value",
            mail.Body,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The language the domain chose — the recipient's when the address has an account, otherwise
    /// the inviter's — has to survive the queue, and <c>null</c> has to stay <c>null</c> so the
    /// renderer applies the product default rather than the domain guessing at one.
    /// </summary>
    [Fact]
    public async Task QueueAsync_CarriesTheChosenLanguage_AndLeavesNullToTheRenderer()
    {
        var outbox = new InMemoryOutbox();
        var mailer = new OutboxInvitationMailer(outbox, TimeProvider.System);

        await mailer.QueueAsync(Invitation);
        await mailer.QueueAsync(Invitation with { Language = null });

        var payloads = outbox.Messages
            .Select(m => JsonDocument.Parse(m.Payload).RootElement)
            .ToArray();

        Assert.Equal("fr-CH", payloads[0].GetProperty("language").GetString());
        Assert.Equal(JsonValueKind.Null, payloads[1].GetProperty("language").ValueKind);
    }

    /// <summary>
    /// Whether the recipient already has an account decides which of the two mails is rendered, and
    /// it is decided here — server-side — because the API never answers that question (NFR-005).
    /// </summary>
    [Fact]
    public async Task QueueAsync_RecordsWhetherTheRecipientHasAnAccount()
    {
        var outbox = new InMemoryOutbox();
        var mailer = new OutboxInvitationMailer(outbox, TimeProvider.System);

        await mailer.QueueAsync(Invitation with { RecipientHasAccount = true });

        var payload = JsonDocument.Parse(Assert.Single(outbox.Messages).Payload).RootElement;

        Assert.True(payload.GetProperty("hasAccount").GetBoolean());
    }
}
