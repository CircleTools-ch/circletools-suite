namespace AppSuite.Circles.Core.Domain;

/// <summary>
/// The reasons a circle operation is refused. These are <em>expected</em> failures — every one of
/// them is something a user can do — so they are returned as values rather than thrown, per the
/// coding guidelines.
/// </summary>
/// <remarks>
/// The API host maps each to a status code and a localized message. Two of them deserve care in
/// that mapping: <see cref="NotAMember"/> and <see cref="InvitationAddressMismatch"/> must answer
/// exactly as "no such thing" does, or they become oracles (NFR-005, NFR-006).
/// </remarks>
public enum CircleError
{
    /// <summary>The caller holds no membership in this circle. Answers as an unknown circle does.</summary>
    NotAMember,

    /// <summary>The caller is a member, but their role does not permit this action.</summary>
    RoleNotPermitted,

    /// <summary>
    /// The acting account no longer resolves — deleted between the token being issued and the
    /// request arriving. Circles hold a user identifier as a value, so this is a case to answer
    /// rather than a broken foreign key.
    /// </summary>
    AccountNotFound,

    /// <summary>The circle's name was empty or whitespace only.</summary>
    NameRequired,

    /// <summary>A person's display name was empty or whitespace only.</summary>
    DisplayNameRequired,

    /// <summary>An email address was empty, or not an address at all.</summary>
    EmailInvalid,

    /// <summary>The caller already owns as many circles as they may (FEAT-003: 20).</summary>
    OwnedCircleLimitReached,

    /// <summary>The caller has created as many circles today as they may (FEAT-003: 10).</summary>
    CircleCreationRateLimitReached,

    /// <summary>The circle already holds as many people as it may (FEAT-003: 100).</summary>
    CircleFull,

    /// <summary>The circle already has as many invitations outstanding as it may (FEAT-003: 50).</summary>
    PendingInvitationLimitReached,

    /// <summary>The inviter or the invited address has hit an invitation rate limit.</summary>
    InvitationRateLimitReached,

    /// <summary>No such invitation — unknown, revoked, consumed, or belonging to another circle.</summary>
    InvitationNotFound,

    /// <summary>The invitation exists but has passed its expiry.</summary>
    InvitationExpired,

    /// <summary>
    /// The caller's verified address is not the invited one. The forwarded-link case: it creates
    /// nothing and must reveal nothing, including whether the invited address has an account.
    /// </summary>
    InvitationAddressMismatch,

    /// <summary>The person is already in this circle.</summary>
    AlreadyAMember,

    /// <summary>No such membership in this circle.</summary>
    MembershipNotFound,

    /// <summary>The membership named as a managed profile is unknown or is account-backed.</summary>
    ManagedMemberNotFound,

    /// <summary>A managed member cannot hold <see cref="CircleRole.Admin"/> or <see cref="CircleRole.Owner"/>.</summary>
    ManagedMemberCannotBeElevated,

    /// <summary>A managed member cannot leave; an owner or admin removes them.</summary>
    ManagedMemberCannotLeave,

    /// <summary>The owner cannot be removed, by anyone, including themselves.</summary>
    OwnerCannotBeRemoved,

    /// <summary>The owner cannot be demoted; ownership moves by transfer only.</summary>
    OwnerCannotBeDemoted,

    /// <summary>Ownership is not granted by a role change. Transfer it instead.</summary>
    OwnershipRequiresTransfer,

    /// <summary>An admin may not remove another admin; only the owner may.</summary>
    AdminCannotRemoveAdmin,

    /// <summary>Removing yourself is leaving, and has its own confirmation and its own rules.</summary>
    CannotRemoveYourself,

    /// <summary>Ownership can only go to someone who can sign in, so never to a managed member.</summary>
    TransferTargetIsManaged,

    /// <summary>The transfer target already holds the ownership being transferred.</summary>
    AlreadyTheOwner,

    /// <summary>
    /// The owner cannot leave while other account-backed people remain — one of them must be made
    /// owner first (FR-021), so the circle is never ownerless.
    /// </summary>
    OwnerMustTransferOwnership,

    /// <summary>
    /// The owner is the only person who could own this circle, so leaving it means deleting it
    /// (FR-021 → FR-022).
    /// </summary>
    OwnerMustDeleteCircle,

    /// <summary>Deletion was confirmed with something other than the circle's name.</summary>
    NameConfirmationMismatch,
}
