// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace CircleTools.SecretSanta.Service.Core.Access;

/// <summary>
/// What a principal may do in an online group (ADR-0086). Endpoints require a permission, never a role, so a new
/// role — co-organizing admins, planned — is a new row in <see cref="RolePermissions"/>, not a change to every
/// endpoint.
/// </summary>
[Flags]
public enum GroupPermissions
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Read the whole group document, its role assignments and its reveal tokens.</summary>
    ReadDocument = 1 << 0,

    /// <summary>Push a new version of the group document.</summary>
    WriteDocument = 1 << 1,

    /// <summary>Add, invite and release participants.</summary>
    ManageParticipants = 1 << 2,

    /// <summary>Turn the join link on or off, rotate it, rotate the organizer link.</summary>
    ManageLinks = 1 << 3,

    /// <summary>Change who organizes the group: hand it over to a link, (later) appoint admins.</summary>
    ManageRoles = 1 << 4,

    /// <summary>Delete the group's online copy.</summary>
    DeleteGroup = 1 << 5,

    /// <summary>Read the trimmed view: one's own pairing per event and one's recipient's wishes.</summary>
    ReadOwnView = 1 << 6,

    /// <summary>Write one's own member entry (gift note, wishlists).</summary>
    WriteOwnEntry = 1 << 7,

    /// <summary>Leave the group.</summary>
    Leave = 1 << 8,
}

/// <summary>Wire names for <see cref="GroupPermissions"/>: the apps decide what to show from these.</summary>
public static class GroupPermissionNames
{
    /// <summary>The camel-cased names of every permission set in <paramref name="permissions"/>.</summary>
    /// <param name="permissions">The permissions.</param>
    /// <returns>The names, in declaration order.</returns>
    public static IReadOnlyList<string> Of(GroupPermissions permissions) =>
        [.. Enum.GetValues<GroupPermissions>()
            .Where(p => p != GroupPermissions.None && permissions.HasFlag(p))
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.ToString()))];
}
