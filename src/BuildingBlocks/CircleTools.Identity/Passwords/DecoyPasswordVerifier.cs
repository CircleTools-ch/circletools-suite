// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Identity;

namespace CircleTools.Identity.Passwords;

/// <summary>
/// Spends the cost of a password hash on a code path that has no password to check, so the path
/// cannot be told apart from one that does — by time (NFR-005).
/// </summary>
/// <remarks>
/// <para>
/// Password hashing is deliberately slow. That is a virtue when a password is being checked and a
/// liability when it is not: a sign-in that skips the hash for an unknown address answers in
/// milliseconds where a real account takes tens of them, and the gap is a reliable oracle for which
/// addresses are registered. Measured on this codebase before the fix, the two sign-in paths differed
/// by <strong>20x</strong>, and the two sign-up paths by <strong>18x</strong> in the opposite
/// direction.
/// </para>
/// <para>
/// Making the fast path slow — rather than the slow path fast — is the only option available:
/// the work on the real path is the security property, so it cannot be removed.
/// </para>
/// <para>
/// The decoy hash is produced by the application's own <see cref="IPasswordHasher{TUser}"/>, so it
/// carries the configured iteration count and costs what a real verification costs. Computing it
/// with a separately constructed hasher would silently drift from the configured work factor and
/// leave a smaller version of the same leak.
/// </para>
/// </remarks>
/// <param name="hasher">The application's password hasher.</param>
public sealed class DecoyPasswordVerifier(IPasswordHasher<CircleToolsUser> hasher)
{
    // Computed once per process rather than per request: this is a constant, and paying for it on
    // every call would make the decoy path measurably SLOWER than the real one - the same leak
    // with its sign flipped. Static because the verifier itself is scoped (IPasswordHasher is),
    // and a singleton holding a scoped dependency is a captive dependency.
    private static string? _decoyHash;
    private static readonly Lock _gate = new();

    private static readonly CircleToolsUser _decoyUser = new()
    {
        Id = Guid.Empty,
        DisplayName = "decoy",
        Email = "decoy@invalid",
        UserName = "decoy@invalid",
    };

    private readonly IPasswordHasher<CircleToolsUser> _hasher = hasher
        ?? throw new ArgumentNullException(nameof(hasher));

    /// <summary>
    /// Performs and discards a password verification.
    /// </summary>
    /// <param name="password">The password the caller supplied. Never stored, never logged.</param>
    /// <remarks>
    /// The result is deliberately ignored — it is meaningless, and the point is the time spent. Call
    /// this on the branch that returns without checking a real password, before writing the response.
    /// </remarks>
    public void Spend(string? password)
    {
        var decoy = _decoyHash;

        if (decoy is null)
        {
            lock (_gate)
            {
                _decoyHash = decoy = _decoyHash
                    ?? _hasher.HashPassword(_decoyUser, Guid.NewGuid().ToString("N"));
            }
        }

        _ = _hasher.VerifyHashedPassword(_decoyUser, decoy, password ?? string.Empty);
    }
}
