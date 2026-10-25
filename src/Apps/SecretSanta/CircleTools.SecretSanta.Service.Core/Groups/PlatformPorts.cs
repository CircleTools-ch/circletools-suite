namespace CircleTools.SecretSanta.Service.Core.Groups;

/// <summary>
/// Secrets for links and keys (ADR-0086): unguessable tokens, stored only as a hash, plus an encrypted copy where
/// the server must hand a token back (ADR-0061). Implemented by the host over the platform's token services.
/// </summary>
public interface IGroupSecrets
{
    /// <summary>Mints a new unguessable token.</summary>
    /// <returns>The token.</returns>
    string NewToken();

    /// <summary>The stored hash of a token.</summary>
    /// <param name="token">The token.</param>
    /// <returns>Its hash.</returns>
    string Hash(string token);

    /// <summary>Encrypts a token for storage.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The ciphertext.</returns>
    string Encrypt(string token);

    /// <summary>Decrypts a stored token.</summary>
    /// <param name="ciphertext">The ciphertext.</param>
    /// <returns>The token.</returns>
    string Decrypt(string ciphertext);
}

/// <summary>An account as an invitation needs it.</summary>
/// <param name="AccountId">The account.</param>
/// <param name="Email">Its verified address, for the invitation mail.</param>
/// <param name="Language">Its preferred language, for the invitation mail.</param>
public sealed record InvitableAccount(Guid AccountId, string Email, string Language);

/// <summary>The platform's accounts, as the group store needs them.</summary>
public interface IGroupAccounts
{
    /// <summary>
    /// Finds the account behind a verified address, taking the same time whether or not one exists (NFR-005).
    /// </summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The account, or <see langword="null"/>.</returns>
    Task<InvitableAccount?> FindByVerifiedEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Whether two accounts share a circle the first one belongs to (FR-027).</summary>
    /// <param name="callerAccountId">The organizer.</param>
    /// <param name="circleId">The circle.</param>
    /// <param name="targetAccountId">The person brought in from it.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when both are in the circle.</returns>
    Task<bool> ShareCircleAsync(Guid callerAccountId, Guid circleId, Guid targetAccountId, CancellationToken cancellationToken = default);
}

/// <summary>What an invitation mail says.</summary>
/// <param name="To">The invited account.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="MemberName">The member the invitation is for.</param>
/// <param name="InviterName">Who invited.</param>
public sealed record GroupInvitationMail(InvitableAccount To, string GroupName, string MemberName, string InviterName);

/// <summary>Sends notifications through the platform's outbox.</summary>
public interface IGroupNotifications
{
    /// <summary>Queues the mail telling an account it was invited to take part in a group.</summary>
    /// <param name="mail">The mail.</param>
    /// <param name="cancellationToken">Cancels the enqueue.</param>
    /// <returns>A task.</returns>
    Task QueueInvitationAsync(GroupInvitationMail mail, CancellationToken cancellationToken = default);
}
