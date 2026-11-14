// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Identity;

namespace CircleTools.Identity;

/// <summary>
/// The suite-wide user account (glossary: <c>User</c>). One account serves every app —
/// there is no per-app login (ADR-0009).
/// </summary>
/// <remarks>
/// Credentials, passkeys and the account lifecycle are owned by ASP.NET Core Identity;
/// this type adds only the profile fields the product reasons about (FR-012).
/// Identity is a <em>supporting</em> subdomain, so there is deliberately no parallel
/// <c>User</c> aggregate duplicating the framework (FEAT-002).
/// </remarks>
public sealed class CircleToolsUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the name shown to everyone sharing a circle with this user.
    /// Deliberately <em>not</em> unique — two "Anna"s in one family is ordinary,
    /// and disambiguation is a UI concern rather than a domain constraint (FR-012).
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's preferred language as a culture name (<c>de-CH</c>,
    /// <c>en</c>, <c>fr-CH</c>). Drives the UI while signed in <em>and</em> the language
    /// of transactional email (NFR-003).
    /// </summary>
    public string PreferredLanguage { get; set; } = "de-CH";

    /// <summary>
    /// Gets the moment the account was created. Used by the retention job that purges
    /// unverified pending accounts after 30 days (NFR-004).
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }
}
