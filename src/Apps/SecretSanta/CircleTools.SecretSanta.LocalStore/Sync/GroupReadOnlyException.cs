// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// Thrown when a group this device holds only through its join link is edited (ADR-0058): a participant
/// sees the group, but only the organizer changes it, and this device has no way to send a group edit.
/// </summary>
public sealed class GroupReadOnlyException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="GroupReadOnlyException"/> class.</summary>
    public GroupReadOnlyException()
        : base("This group can only be changed by its organizer.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GroupReadOnlyException"/> class.</summary>
    /// <param name="groupId">The read-only group.</param>
    public GroupReadOnlyException(Guid groupId)
        : base($"Group {groupId} can only be changed by its organizer.")
    {
        GroupId = groupId;
    }

    /// <summary>Initializes a new instance of the <see cref="GroupReadOnlyException"/> class.</summary>
    /// <param name="message">The message.</param>
    public GroupReadOnlyException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GroupReadOnlyException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public GroupReadOnlyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the read-only group, when known.</summary>
    public Guid GroupId { get; }
}
