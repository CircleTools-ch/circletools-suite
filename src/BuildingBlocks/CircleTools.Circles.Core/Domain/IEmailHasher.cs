// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// Reduces an email address to the opaque hash the Circles context stores and compares (ADR-0062,
/// NFR-004). A <em>keyed</em> HMAC, not a bare digest: the address space is small and enumerable, so an
/// unsalted digest is reversible by dictionary attack. Without the server key an attacker holding the
/// database cannot confirm a guessed address against a stored hash.
/// </summary>
/// <remarks>
/// The hash is computed at the application boundary and handed into the aggregate as a value, so the
/// domain never holds the key — the same shape the pre-hashed invitation secret handle already uses.
/// </remarks>
public interface IEmailHasher
{
    /// <summary>Computes the stored hash of an address, ignoring case and surrounding whitespace.</summary>
    /// <param name="email">The address, in any casing.</param>
    /// <returns>The stored hash of the address.</returns>
    string Hash(string email);
}
