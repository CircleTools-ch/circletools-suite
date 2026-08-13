using CircleTools.SharedKernel;

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// A standing offer to join a circle, sent to an email address (FR-017). It lives inside the
/// circle, is single-use, and expires.
/// </summary>
/// <remarks>
/// This is the only place in M2 that stores an address belonging to someone who has not agreed to
/// anything, so it holds exactly what sending and resending need, and it is <em>deleted</em> — not
/// flagged — the moment it is consumed, revoked or expired (NFR-004).
/// </remarks>
public sealed class Invitation : Entity
{
    // EF Core materialization constructor.
    private Invitation()
    {
        Email = string.Empty;
        EmailHash = string.Empty;
        SecretHandle = string.Empty;
    }

    private Invitation(
        Guid id,
        string email,
        string emailHash,
        Guid invitedByUserId,
        Guid? managedMembershipId,
        string secretHandle,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        Email = email;
        EmailHash = emailHash;
        InvitedByUserId = invitedByUserId;
        ManagedMembershipId = managedMembershipId;
        SecretHandle = secretHandle;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>Gets the invited address, as the inviter typed it. Needed to send and resend.</summary>
    public string Email { get; private set; }

    /// <summary>Gets the hash of the invited address — what everything else compares against.</summary>
    public string EmailHash { get; private set; }

    /// <summary>Gets the user who issued the invitation.</summary>
    public Guid InvitedByUserId { get; private set; }

    /// <summary>
    /// Gets the managed membership this invitation promotes, when it was sent from a managed
    /// profile (FR-019). On acceptance that membership is linked instead of a second one appearing.
    /// </summary>
    public Guid? ManagedMembershipId { get; private set; }

    /// <summary>Gets the stored handle of the link's secret. The secret itself is never stored.</summary>
    public string SecretHandle { get; private set; }

    /// <summary>Gets the moment the invitation was first created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the moment the invitation stops being acceptable.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Checks whether the invitation has passed its expiry.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns><c>true</c> when it can no longer be accepted.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAt;

    internal static Invitation Issue(
        string email,
        Guid invitedByUserId,
        Guid? managedMembershipId,
        string secretHandle,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        new(
            GuidV7.New(),
            EmailAddress.Normalize(email),
            EmailAddress.HashOf(email),
            invitedByUserId,
            managedMembershipId,
            secretHandle,
            now,
            now + lifetime);

    /// <summary>
    /// Re-issues the link: a fresh secret and a fresh window, and the previous link stops working.
    /// One invitation, one live link, always — so revoking or letting it lapse cannot be defeated
    /// by an older mail still sitting in an inbox.
    /// </summary>
    internal void Reissue(string secretHandle, Guid invitedByUserId, DateTimeOffset now, TimeSpan lifetime)
    {
        SecretHandle = secretHandle;
        InvitedByUserId = invitedByUserId;
        ExpiresAt = now + lifetime;
    }

    /// <summary>Records that the invitation now targets a managed profile for promotion.</summary>
    internal void RetargetTo(Guid? managedMembershipId) => ManagedMembershipId = managedMembershipId;
}
