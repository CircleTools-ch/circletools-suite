// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Contracts;

/// <summary>A group in an account's overview (ADR-0086): every group the account has a role in.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="GroupName">The group's name.</param>
/// <param name="Role">The account's role (camel-cased: <c>owner</c>, <c>participant</c>; more later).</param>
/// <param name="Invited">Whether this is an invitation still waiting for an answer.</param>
/// <param name="MemberId">The roster member the account is, if any.</param>
/// <param name="MemberName">That member's name, if any.</param>
/// <param name="Version">The document version.</param>
/// <param name="Permissions">What the account may do there (camel-cased permission names).</param>
public sealed record MyGroup(
    Guid GroupId,
    string GroupName,
    string Role,
    bool Invited,
    Guid? MemberId,
    string? MemberName,
    long Version,
    IReadOnlyList<string> Permissions);

/// <summary>The account's overview.</summary>
/// <param name="Groups">The groups.</param>
public sealed record MyGroups(IReadOnlyList<MyGroup> Groups);

/// <summary>A role assignment as the organizer sees it.</summary>
/// <param name="Role">The role (camel-cased).</param>
/// <param name="Principal">The principal kind (camel-cased: <c>account</c>, <c>organizerLink</c>, <c>deviceKey</c>).</param>
/// <param name="MemberId">The roster member, if any.</param>
/// <param name="Invited">Whether it waits for an answer.</param>
/// <param name="Route">How it came about (camel-cased).</param>
public sealed record GroupRoleView(string Role, string Principal, Guid? MemberId, bool Invited, string Route);

/// <summary>How a person is brought into a group by its organizer.</summary>
public enum ParticipantRoute
{
    /// <summary>The organizer's own member, connected to the organizer's account.</summary>
    OwnMember = 1,

    /// <summary>An email invitation the invited account accepts.</summary>
    Invitation = 2,

    /// <summary>A person from a circle the organizer shares with them.</summary>
    Circle = 3,
}

/// <summary>Brings a person into an online group as one of its members (ADR-0086).</summary>
/// <param name="MemberId">The member.</param>
/// <param name="Route">The route.</param>
/// <param name="Email">For an invitation, the address.</param>
/// <param name="CircleId">For a circle, the shared circle.</param>
/// <param name="AccountId">For a circle, the person's account.</param>
public sealed record AddParticipantRequest(
    Guid MemberId,
    ParticipantRoute Route,
    string? Email = null,
    Guid? CircleId = null,
    Guid? AccountId = null);

/// <summary>Takes part in a group through its join link.</summary>
/// <param name="MemberId">An existing member to take part as; or <see langword="null"/> with <paramref name="NewName"/>.</param>
/// <param name="NewName">The name to add oneself under, when not picking an existing member.</param>
/// <param name="WithAccount">Whether to take part with the signed-in account (all its devices) or this device only.</param>
public sealed record JoinRequest(Guid? MemberId, string? NewName, bool WithAccount);

/// <summary>The answer to a join.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="MemberId">The member taken part as.</param>
/// <param name="DeviceKey">For a device-only join, the key this device proves itself with from now on.</param>
public sealed record JoinResponse(Guid GroupId, Guid MemberId, string? DeviceKey);

/// <summary>A participant's own entry, as written.</summary>
/// <param name="GiftNote">The gift note; blank clears it.</param>
/// <param name="Wishlists">One wishlist per event; empty items clear it.</param>
public sealed record OwnEntryRequest(string? GiftNote, IReadOnlyList<OwnWishlist> Wishlists);

/// <summary>One event's wishlist in an own entry.</summary>
/// <param name="EventId">The event.</param>
/// <param name="Items">The items.</param>
public sealed record OwnWishlist(Guid EventId, IReadOnlyList<string> Items);

/// <summary>What a reveal-link holder may edit: their member's gift note and that event's wishlist (ADR-0084).</summary>
/// <param name="GiftNote">The gift note, if any.</param>
/// <param name="Items">The wishlist for the link's event.</param>
public sealed record RevealEntry(string? GiftNote, IReadOnlyList<string> Items);

/// <summary>A reveal token as the organizer gets it, to build short reveal links (ADR-0082).</summary>
/// <param name="EventId">The event.</param>
/// <param name="GiverMemberId">The giver.</param>
/// <param name="Token">The token.</param>
public sealed record RevealTokenView(Guid EventId, Guid GiverMemberId, string Token);

/// <summary>The links an organizer holds for an online group.</summary>
/// <param name="OrganizerToken">The organizer link's token, for a link-owned group.</param>
/// <param name="JoinToken">The join link's token, while the join link is on.</param>
public sealed record GroupLinks(string? OrganizerToken, string? JoinToken);

/// <summary>Makes a group online (ADR-0086).</summary>
/// <param name="SchemaVersion">The document's schema version.</param>
/// <param name="Document">The group document (the <c>GroupDocument</c> format).</param>
/// <param name="CreatorKey">A secret the creating device keeps, required without an account: a retried create recovers its links with it.</param>
/// <param name="JoinEnabled">Whether to turn the join link on right away.</param>
public sealed record CreateOnlineGroupRequest(int SchemaVersion, System.Text.Json.JsonElement Document, string? CreatorKey, bool JoinEnabled);

/// <summary>The whole online group, for a caller who may read the document.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Version">The document version.</param>
/// <param name="SchemaVersion">The document schema version.</param>
/// <param name="Document">The document, member entries merged in.</param>
/// <param name="JoinEnabled">Whether the join link lets people in.</param>
/// <param name="Roles">Who has a role in the group.</param>
/// <param name="SelfMaintained">Members who keep their own entry (their gift note and wishlists are read-only for the organizer).</param>
/// <param name="Permissions">What the caller may do (camel-cased permission names).</param>
/// <param name="Links">The organizer and join links.</param>
public sealed record OrganizerGroupView(
    Guid GroupId,
    long Version,
    int SchemaVersion,
    System.Text.Json.JsonElement Document,
    bool JoinEnabled,
    IReadOnlyList<GroupRoleView> Roles,
    IReadOnlyList<Guid> SelfMaintained,
    IReadOnlyList<string> Permissions,
    GroupLinks Links);

/// <summary>Pushes a new version of the document.</summary>
/// <param name="BaseVersion">The version the pusher's copy was based on.</param>
/// <param name="SchemaVersion">The document's schema version.</param>
/// <param name="Document">The document.</param>
public sealed record PushOnlineGroupRequest(long BaseVersion, int SchemaVersion, System.Text.Json.JsonElement Document);

/// <summary>The answer to a push.</summary>
/// <param name="Version">The stored version.</param>
/// <param name="Rewritten">Whether the stored document differs from the pushed one (members' own entries re-applied): adopt <paramref name="Document"/>.</param>
/// <param name="Document">The stored document.</param>
public sealed record PushOnlineGroupResponse(long Version, bool Rewritten, System.Text.Json.JsonElement Document);

/// <summary>Turns the join link on or off, optionally replacing it.</summary>
/// <param name="Enabled">On or off.</param>
/// <param name="Rotate">Whether to replace the link (the old one stops working).</param>
public sealed record JoinLinkRequest(bool Enabled, bool Rotate = false);

/// <summary>The answer to an own-entry write.</summary>
/// <param name="Version">The document version now stored.</param>
public sealed record OwnEntryResponse(long Version);
