// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Domain;

/// <summary>What the Circles context needs to know about an account, and nothing more.</summary>
/// <param name="UserId">The account's identity.</param>
/// <param name="DisplayName">The name others see.</param>
/// <param name="Email">The account's verified address.</param>
/// <param name="Language">The account's preferred language, when it has one.</param>
public sealed record CircleAccount(Guid UserId, string DisplayName, string Email, string? Language);

/// <summary>
/// The Circles context's read-only window onto accounts.
/// </summary>
/// <remarks>
/// Circles does not reference <c>CircleTools.Identity</c>: a membership holds a user identifier as a
/// <em>value</em>, and an account that no longer resolves is a case the context tolerates rather
/// than a broken foreign key. This port is the one place that crosses over, and the API host
/// implements it.
/// <para>
/// Only <em>verified</em> addresses resolve. A membership is granted only to a proven owner of the
/// invited address (FR-018), so an unverified match must look exactly like no match at all.
/// </para>
/// </remarks>
public interface IAccountDirectory
{
    /// <summary>Finds the account whose verified address this is.</summary>
    /// <param name="email">The address, in any casing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account, or <c>null</c> when no verified account holds that address.</returns>
    Task<CircleAccount?> FindByVerifiedEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Finds an account by its identity.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The account, or <c>null</c> when it no longer exists <em>or its address is not verified</em>.
    /// An unverified account gets nothing account-scoped, so as far as circles are concerned it is
    /// not there at all — which keeps the "verified address" rule out of every call site.
    /// </returns>
    Task<CircleAccount?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}
