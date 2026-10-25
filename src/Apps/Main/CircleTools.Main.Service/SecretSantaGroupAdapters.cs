using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CircleTools.Circles.Core.Application;
using CircleTools.Circles.Core.Domain;
using CircleTools.Infrastructure.Mail;
using CircleTools.Infrastructure.Outbox;
using CircleTools.Main.Service.Core.Domain;
using CircleTools.SecretSanta.Service.Core.Groups;

namespace CircleTools.Main.Service;

/// <summary>
/// Link and key secrets for the Secret-Santa group store (ADR-0086) over the platform's token services: the same
/// entropy and at-rest encryption as every other share token (ADR-0061), hashed with SHA-256 for lookup.
/// </summary>
/// <param name="factory">The platform's token factory.</param>
/// <param name="cipher">The platform's token cipher.</param>
public sealed class SecretSantaGroupSecrets(IShareTokenFactory factory, IShareTokenCipher cipher) : IGroupSecrets
{
    /// <inheritdoc />
    public string NewToken() => factory.Create();

    /// <inheritdoc />
    public string Hash(string token) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <inheritdoc />
    public string Encrypt(string token) => cipher.Encrypt(token);

    /// <inheritdoc />
    public string Decrypt(string ciphertext) => cipher.Decrypt(ciphertext);
}

/// <summary>The platform's accounts and circles, as the Secret-Santa group store needs them (ADR-0086).</summary>
/// <param name="directory">The account directory (constant-time address lookup, NFR-005).</param>
/// <param name="sender">The application dispatcher, for the authorized circle read.</param>
public sealed class SecretSantaGroupAccounts(IAccountDirectory directory, CircleTools.Application.ISender sender) : IGroupAccounts
{
    /// <inheritdoc />
    public async Task<InvitableAccount?> FindByVerifiedEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var account = await directory.FindByVerifiedEmailAsync(email, cancellationToken).ConfigureAwait(false);
        return account is null ? null : new InvitableAccount(account.UserId, account.Email, account.Language ?? "en");
    }

    /// <inheritdoc />
    public async Task<bool> ShareCircleAsync(Guid callerAccountId, Guid circleId, Guid targetAccountId, CancellationToken cancellationToken = default)
    {
        // The authorized circle read proves the caller is in the circle and lists its people with their accounts.
        var circle = await sender.Send(new GetCircleQuery(callerAccountId, circleId), cancellationToken).ConfigureAwait(false);
        return circle.IsSuccess && circle.Value!.People.Any(p => p.AccountId == targetAccountId);
    }
}

/// <summary>Queues Secret-Santa group mails through the platform outbox (ADR-0064 branding).</summary>
/// <param name="outbox">The outbox.</param>
/// <param name="clock">The clock.</param>
public sealed class SecretSantaGroupNotifications(IOutbox outbox, TimeProvider clock) : IGroupNotifications
{
    /// <inheritdoc />
    public async Task QueueInvitationAsync(GroupInvitationMail mail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mail);

        var payload = JsonSerializer.Serialize(new
        {
            email = mail.To.Email,
            language = mail.To.Language,
            groupName = mail.GroupName,
            memberDisplayName = mail.MemberName,
            inviterDisplayName = mail.InviterName,
            sourceApp = "secretsanta",
        });

        await outbox.EnqueueAsync(OutboxMessage.Create(MailTemplateRenderer.MemberLinkType, payload, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Account deletion for the Secret-Santa group store (ADR-0022, FR-043): owned online groups are handed over to an
/// organizer link (the account endpoint deletes them first when the user chose that); every other role goes.
/// </summary>
/// <param name="service">The group service.</param>
public sealed class SecretSantaGroupAccountCleanup(GroupService service) : IAccountDeletionCleanup
{
    /// <inheritdoc />
    public Task CleanUpAsync(Guid userId, CancellationToken cancellationToken = default) =>
        service.CleanUpAccountAsync(userId, deleteOwnedGroups: false, cancellationToken);
}
