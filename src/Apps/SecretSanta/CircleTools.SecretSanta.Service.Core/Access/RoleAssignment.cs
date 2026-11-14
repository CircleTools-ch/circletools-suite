// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Service.Core.Access;

/// <summary>A role a principal can hold in an online group (ADR-0086).</summary>
/// <remarks>
/// Co-organizing admins are planned: they become a new value here and a new row in <see cref="RolePermissions"/>.
/// Values are persisted, so they never change meaning.
/// </remarks>
public enum GroupRole
{
    /// <summary>Organizes the group: the whole document, participants, links, deletion and hand-over.</summary>
    Owner = 1,

    /// <summary>Takes part: their own pairing and their own entry.</summary>
    Participant = 2,
}

/// <summary>Who holds a role assignment (ADR-0086).</summary>
public enum PrincipalKind
{
    /// <summary>A signed-in account: the group is on all its devices.</summary>
    Account = 1,

    /// <summary>The organizer link, a token held by every device it was opened on (account-free organizing).</summary>
    OrganizerLink = 2,

    /// <summary>A key one device was given when it joined without an account.</summary>
    DeviceKey = 3,
}

/// <summary>Whether a role assignment is in force.</summary>
public enum RoleAssignmentState
{
    /// <summary>Waiting for the invited account to accept; grants nothing yet.</summary>
    Invited = 1,

    /// <summary>In force.</summary>
    Active = 2,
}

/// <summary>How a role assignment came about — kept for display and support, never for authorization.</summary>
public enum AssignmentRoute
{
    /// <summary>The group was made online by this principal.</summary>
    Created = 1,

    /// <summary>The person picked themselves through the join link.</summary>
    JoinLink = 2,

    /// <summary>The organizer invited an email address and the account accepted.</summary>
    Invitation = 3,

    /// <summary>The organizer brought the person in from a shared circle.</summary>
    Circle = 4,

    /// <summary>The organizer connected their own account to their own member.</summary>
    OwnMember = 5,

    /// <summary>An account took over a link-owned group.</summary>
    TakeOver = 6,
}

/// <summary>
/// One principal's role in one online group, optionally as one member of its roster (ADR-0086). The organizer who
/// is also in the roster holds a single assignment: role owner, plus their member.
/// </summary>
/// <param name="Id">The assignment's id.</param>
/// <param name="GroupId">The group.</param>
/// <param name="Role">The role.</param>
/// <param name="PrincipalKind">What kind of principal holds it.</param>
/// <param name="AccountId">The account, for an account principal.</param>
/// <param name="MemberId">The roster member this principal is, if any.</param>
/// <param name="State">Whether it is in force.</param>
/// <param name="Route">How it came about.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="ExpiresAt">For an invitation, when it lapses.</param>
public sealed record RoleAssignment(
    Guid Id,
    Guid GroupId,
    GroupRole Role,
    PrincipalKind PrincipalKind,
    Guid? AccountId,
    Guid? MemberId,
    RoleAssignmentState State,
    AssignmentRoute Route,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt = null)
{
    /// <summary>Gets what this assignment allows: the role's permissions (plus a member's, with a member) while active.</summary>
    public GroupPermissions Permissions => State != RoleAssignmentState.Active
        ? GroupPermissions.None
        : RolePermissions.For(Role) | (MemberId is null ? GroupPermissions.None : RolePermissions.Member);

    /// <summary>Whether this assignment allows <paramref name="permission"/>.</summary>
    /// <param name="permission">The permission to check.</param>
    /// <returns><see langword="true"/> when every flag in <paramref name="permission"/> is granted.</returns>
    public bool Allows(GroupPermissions permission) => permission != GroupPermissions.None && Permissions.HasFlag(permission);
}
