// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Account;

/// <summary>
/// Told when the signed-in account was deleted from this device (FR-043), so an app can settle what it keeps locally
/// for that account. Optional: a host registers none, one or several.
/// </summary>
/// <remarks>
/// The server data is gone by then and the session is about to be forgotten; a handler works on this device only.
/// "Data on your own devices stays untouched" is the promise the deletion page makes, so a handler keeps what the
/// person made and drops only what can no longer be reached.
/// </remarks>
public interface IAccountDeletedHandler
{
    /// <summary>Settles this device's data for the deleted account.</summary>
    /// <param name="accountId">The deleted account.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once this device's data is settled.</returns>
    Task OnAccountDeletedAsync(Guid accountId, CancellationToken cancellationToken = default);
}
