// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Contracts;

/// <summary>Creation of a circle (FR-016).</summary>
/// <param name="Name">Identifies the circle to its members. Required.</param>
/// <param name="Label">
/// The word the UI uses for this circle. Pre-filled with the creator's language default
/// (<em>Familie</em> / <em>Family</em> / <em>Famille</em>) and stored as user data — never
/// re-translated. Blank falls back to that default.
/// </param>
public sealed record CreateCircleRequest(string Name, string? Label);

/// <summary>Edit of a circle's name and label (FR-020). Owner and admin only.</summary>
/// <param name="Name">The new name. Required.</param>
/// <param name="Label">The new label. Blank falls back to the actor's language default.</param>
public sealed record UpdateCircleRequest(string Name, string? Label);

/// <summary>
/// Confirmation of circle deletion (FR-022). No password: the threat here is the misclick, so the
/// owner types the circle's name back instead.
/// </summary>
/// <param name="ConfirmedName">The circle's name as typed by the owner.</param>
public sealed record DeleteCircleRequest(string ConfirmedName);

/// <summary>A circle as it appears in the caller's list of circles (FR-016).</summary>
/// <param name="Id">The circle's identity.</param>
/// <param name="Name">The circle's name.</param>
/// <param name="Label">The stored label, in the language it was written in.</param>
/// <param name="Role">The caller's role: <c>owner</c>, <c>admin</c> or <c>member</c>.</param>
/// <param name="PeopleCount">How many people are in the circle, managed members included.</param>
public sealed record CircleSummaryResponse(Guid Id, string Name, string Label, string Role, int PeopleCount);

/// <summary>A circle and its people, as seen by a member (FR-016, FR-020).</summary>
/// <param name="Id">The circle's identity.</param>
/// <param name="Name">The circle's name.</param>
/// <param name="Label">The stored label, in the language it was written in.</param>
/// <param name="Role">The caller's role: <c>owner</c>, <c>admin</c> or <c>member</c>.</param>
/// <param name="People">The people list — display names only, never addresses (NFR-006).</param>
public sealed record CircleDetailResponse(
    Guid Id,
    string Name,
    string Label,
    string Role,
    IReadOnlyList<CirclePersonResponse> People);

/// <summary>
/// A person in a circle's people list (FR-019, FR-020). Carries a display name and nothing that
/// identifies the person outside the circle — no email address, by
/// <see href="../../docs/requirements/non-functional/NFR-006-circle-authorization.md">NFR-006</see>.
/// </summary>
/// <param name="MembershipId">The membership's identity — the handle for role, removal and transfer.</param>
/// <param name="DisplayName">The name the person is known by in this circle.</param>
/// <param name="Role">Their role: <c>owner</c>, <c>admin</c> or <c>member</c>.</param>
/// <param name="IsManaged">
/// True for a managed member — someone with a profile but no account, who can never sign in.
/// </param>
/// <param name="IsYou">True when this membership is the caller's own.</param>
/// <param name="AccountId">
/// The opaque account identifier of an account-backed member, or <see langword="null"/> for a managed
/// member. It is shown only to fellow circle members (the endpoint is member-authorized) and carries
/// no address — NFR-005's "does this address have an account" oracle is untouched. It exists so a
/// group created <em>from</em> this circle (FR-027, M4) can link an account-backed person it already
/// shares the circle with, which is the established relationship ADR-0018 is built on. Never an email.
/// </param>
public sealed record CirclePersonResponse(
    Guid MembershipId,
    string DisplayName,
    string Role,
    bool IsManaged,
    bool IsYou,
    Guid? AccountId = null);

/// <summary>Addition of a managed member (FR-019). Owner and admin only.</summary>
/// <param name="DisplayName">Required. Not unique — two people may share a name.</param>
/// <param name="Language">Optional preferred language. Nothing else is stored about them.</param>
public sealed record AddManagedMemberRequest(string DisplayName, string? Language);

/// <summary>Edit of a managed member's profile (FR-019).</summary>
/// <param name="DisplayName">The new display name. Required.</param>
/// <param name="Language">The new preferred language, or <c>null</c> to clear it.</param>
public sealed record UpdateManagedMemberRequest(string DisplayName, string? Language);

/// <summary>Role change for a membership (FR-020). Owner only, and never to <c>owner</c>.</summary>
/// <param name="Role">The new role: <c>admin</c> or <c>member</c>.</param>
public sealed record ChangeRoleRequest(string Role);

/// <summary>
/// A circle the caller is the sole owner of, listed when something they asked for is blocked by
/// that (FR-015's deletion guard, FR-021's leave refusal).
/// </summary>
/// <param name="Id">The circle's identity.</param>
/// <param name="Name">The circle's name, so the user can act on the right one.</param>
/// <param name="Label">The circle's label.</param>
/// <param name="OtherAccountBackedPeople">
/// How many other people in it could receive ownership. Zero means deletion is the only way out.
/// </param>
public sealed record SoleOwnedCircleResponse(Guid Id, string Name, string Label, int OtherAccountBackedPeople);
