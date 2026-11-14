// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Identity.Tokens;

/// <summary>
/// Minimal user lookup needed to mint an access token during refresh.
/// </summary>
/// <remarks>
/// Deliberately narrower than <c>UserManager</c>: the session path needs to read one user, and a
/// small port keeps <see cref="SessionService"/> unit-testable without a database or the whole
/// Identity stack.
/// </remarks>
public interface ISessionUserLookup
{
    /// <summary>Finds the user a session belongs to.</summary>
    /// <param name="userId">The user's identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user, or <c>null</c> if the account no longer exists.</returns>
    Task<CircleToolsUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
