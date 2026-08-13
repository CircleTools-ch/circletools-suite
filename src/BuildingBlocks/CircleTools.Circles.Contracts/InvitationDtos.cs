namespace CircleTools.Circles.Contracts;

/// <summary>Invitation of a person to a circle by email address (FR-017). Owner and admin only.</summary>
/// <param name="Email">The address to invite.</param>
/// <param name="ManagedMembershipId">
/// Set when inviting an existing <em>managed member</em> who has since obtained an address
/// (FR-019). On acceptance their membership is linked rather than duplicated.
/// </param>
public sealed record InviteRequest(string Email, Guid? ManagedMembershipId);

/// <summary>
/// A pending invitation as shown to an owner or admin (FR-017), so they can tell one from another
/// and resend or revoke it.
/// </summary>
/// <param name="Id">The invitation's identity.</param>
/// <param name="InvitedEmail">
/// The address that was invited. This is the one address a circle endpoint returns, and it is not
/// the case NFR-006 forbids: it is an address an owner or admin typed into this circle, and it says
/// nothing about whether that address has an account — which is the property NFR-005 protects.
/// No <em>member's</em> address is ever returned.
/// </param>
/// <param name="InvitedAt">When the invitation was first created.</param>
/// <param name="ExpiresAt">When it stops being acceptable and leaves this list.</param>
public sealed record PendingInvitationResponse(
    Guid Id,
    string InvitedEmail,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// What the holder of an invitation link is shown before accepting (FR-018) — the circle's name,
/// its label and the inviter's display name, and deliberately nothing else.
/// </summary>
/// <remarks>
/// In particular it does not say whether the invited address has an account. The recipient knows;
/// anyone the link was forwarded to must not learn it (ADR-0018, NFR-005), so the landing page
/// offers both signing in and signing up and lets the recipient's own side of the branch resolve it.
/// </remarks>
/// <param name="CircleName">The circle's name.</param>
/// <param name="CircleLabel">The circle's label, in the language it was written in.</param>
/// <param name="InviterDisplayName">The display name of whoever sent the invitation.</param>
public sealed record InvitationPreviewResponse(string CircleName, string CircleLabel, string InviterDisplayName);

/// <summary>
/// An invitation waiting for the signed-in caller, whose verified address it was sent to (FR-018).
/// It is accepted or declined in the app, with no token from the mail.
/// </summary>
/// <param name="CircleId">The circle being joined.</param>
/// <param name="InvitationId">The invitation to accept or decline.</param>
/// <param name="CircleName">The circle's name.</param>
/// <param name="CircleLabel">The circle's label.</param>
/// <param name="InviterDisplayName">The display name of whoever sent it.</param>
public sealed record JoinRequestResponse(
    Guid CircleId,
    Guid InvitationId,
    string CircleName,
    string CircleLabel,
    string InviterDisplayName);
