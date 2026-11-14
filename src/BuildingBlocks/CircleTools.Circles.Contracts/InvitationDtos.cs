// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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
/// <param name="ManagedMembershipId">
/// The managed member this invitation promotes, when the inviter aimed it at one (FR-019), so the
/// admin can tell "invite a new person" from "invite Grandma to take over her offline profile". Null
/// for an ordinary invitation. Resolved to the profile's <em>current</em> name in
/// <paramref name="ManagedDisplayName"/>; a target removed since the invitation was sent reads as null
/// in both, and the invitation then behaves as an ordinary one.
/// </param>
/// <param name="ManagedDisplayName">The target managed member's current display name, or null.</param>
public sealed record PendingInvitationResponse(
    Guid Id,
    string InvitedEmail,
    DateTimeOffset InvitedAt,
    DateTimeOffset ExpiresAt,
    Guid? ManagedMembershipId = null,
    string? ManagedDisplayName = null);

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

/// <summary>
/// One offline person (managed member) the recipient of an invitation may join <em>as</em> (FR-019).
/// Carries a display name and the membership id and nothing else — no address, and never an
/// account-backed member.
/// </summary>
/// <param name="MembershipId">The managed membership's identity, sent back as the recipient's choice.</param>
/// <param name="DisplayName">The name that person is known by in the circle.</param>
public sealed record ManagedPersonOption(Guid MembershipId, string DisplayName);

/// <summary>
/// What the invited person is offered when deciding <em>who</em> they are joining as (FR-019). It is
/// returned only to a signed-in caller whose <strong>verified</strong> address is the invited one —
/// the same check that admits acceptance — so the offline roster it carries is never exposed to a
/// forwarded-link holder or shown on the anonymous landing preview (ADR-0043, NFR-006).
/// </summary>
/// <param name="PreselectedMembershipId">
/// The managed member the inviter already aimed the invitation at, when they did. When set, the
/// recipient has no choice to make — they will take over that profile — and <paramref name="Options"/>
/// is empty. When null, the recipient may pick from <paramref name="Options"/> or join as new.
/// </param>
/// <param name="PreselectedDisplayName">That target's display name, or null.</param>
/// <param name="Options">
/// The circle's offline people the recipient may claim to be, present only for an untargeted
/// invitation. Empty when the inviter targeted someone, or when the circle has no offline people.
/// </param>
public sealed record JoinTargetsResponse(
    Guid? PreselectedMembershipId,
    string? PreselectedDisplayName,
    IReadOnlyList<ManagedPersonOption> Options);

/// <summary>
/// The body of an accept, carrying the recipient's self-selected offline profile (FR-019). Optional
/// on both accept routes: an ordinary accept sends none, and an invitation the inviter already aimed
/// at a managed member ignores it — the inviter's choice wins.
/// </summary>
/// <param name="ManagedMembershipId">The offline person the recipient chose to become, or null to join as new.</param>
public sealed record AcceptInvitationBody(Guid? ManagedMembershipId);
