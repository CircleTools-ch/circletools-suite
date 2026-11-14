// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Thrown when a member name would duplicate an existing member of the same group (case-insensitive).
/// Derives from <see cref="InvalidOperationException"/> so existing catch-all handling keeps working,
/// but carries the offending <see cref="Name"/> so the UI can render a <em>localized</em> message
/// instead of surfacing this (English) exception text.
/// </summary>
public sealed class DuplicateMemberNameException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="DuplicateMemberNameException"/> class.</summary>
    /// <param name="name">The duplicate member name.</param>
    public DuplicateMemberNameException(string name)
        : base($"A member named '{name}' already exists in this group.") => Name = name;

    /// <summary>Gets the member name that caused the conflict.</summary>
    public string Name { get; }
}
