// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Entries;

namespace CircleTools.SecretSanta.Service.Repository;

/// <summary>An online group's one server copy (ADR-0086).</summary>
public sealed class GroupRow
{
    /// <summary>Gets or sets the group id (the document's own id).</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the document version, raised on every change.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets the document schema version of the last write.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Gets or sets the document (jsonb, the <c>GroupDocument</c> format), member entries merged in.</summary>
    public string Body { get; set; } = "{}";

    /// <summary>Gets or sets whether the join link lets people in.</summary>
    public bool JoinEnabled { get; set; }

    /// <summary>Gets or sets the creator key's hash, so a retried create can recover its links.</summary>
    public string? CreatorKeyHash { get; set; }

    /// <summary>Gets or sets the role assignment that last wrote the document.</summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>Gets or sets when the group was made online.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the document last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets when anyone last used the group, for retention (NFR-010).</summary>
    public DateTimeOffset LastActivityAt { get; set; }
}

/// <summary>One principal's role in a group (ADR-0086).</summary>
public sealed class RoleAssignmentRow
{
    /// <summary>Gets or sets the assignment id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the role.</summary>
    public GroupRole Role { get; set; }

    /// <summary>Gets or sets the principal kind.</summary>
    public PrincipalKind PrincipalKind { get; set; }

    /// <summary>Gets or sets the account, for an account principal.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Gets or sets the hash of the organizer token or device key, for a principal with a secret.</summary>
    public string? SecretHash { get; set; }

    /// <summary>Gets or sets the organizer token, encrypted, so the organizer's devices can be handed it again.</summary>
    public string? SecretCipher { get; set; }

    /// <summary>Gets or sets the roster member this principal is, if any.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>Gets or sets whether it is in force.</summary>
    public RoleAssignmentState State { get; set; }

    /// <summary>Gets or sets how it came about.</summary>
    public AssignmentRoute Route { get; set; }

    /// <summary>Gets or sets when it was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when an invitation lapses.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>The join link of a group (ADR-0086); a rotated link stays as an inactive row.</summary>
public sealed class JoinLinkRow
{
    /// <summary>Gets or sets the token's hash (the key; the token itself is never stored in clear).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the token, encrypted, so the organizer's devices can show the link again.</summary>
    public string TokenCipher { get; set; } = string.Empty;

    /// <summary>Gets or sets the group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets whether this is the group's current link.</summary>
    public bool Active { get; set; }

    /// <summary>Gets or sets when it was minted.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>What a member keeps themselves (ADR-0086).</summary>
public sealed class MemberEntryRow
{
    /// <summary>Gets or sets the group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the member.</summary>
    public Guid MemberId { get; set; }

    /// <summary>Gets or sets the gift note.</summary>
    public string? GiftNote { get; set; }

    /// <summary>Gets or sets the wishlists per event (jsonb).</summary>
    public string Wishlists { get; set; } = "[]";

    /// <summary>Gets or sets who keeps it.</summary>
    public MemberEntryKeeper KeptBy { get; set; }

    /// <summary>Gets or sets the entry's version, raised on every write.</summary>
    public long Version { get; set; }

    /// <summary>Gets or sets when it was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A giver's reveal token in one event (ADR-0086): the page it opens is derived on read.</summary>
public sealed class RevealTokenRow
{
    /// <summary>Gets or sets the token's hash (the key).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the token, encrypted, so the organizer's devices can rebuild short links.</summary>
    public string TokenCipher { get; set; } = string.Empty;

    /// <summary>Gets or sets the group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>Gets or sets the event.</summary>
    public Guid EventId { get; set; }

    /// <summary>Gets or sets the giver.</summary>
    public Guid GiverMemberId { get; set; }

    /// <summary>Gets or sets when it was minted.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
