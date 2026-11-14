// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// How circles handle email addresses: the trimmed form is kept only where it is needed to send
/// mail, and everywhere else an address is reduced to a hash.
/// </summary>
/// <remarks>
/// Hashing is what lets a declined address be remembered for the cooldown without the circle
/// keeping an address it has no further business with (NFR-004), and it is what makes
/// "is this the invited address?" answerable without comparing addresses in the clear. The hash
/// itself is computed by <see cref="IEmailHasher"/> at the application boundary (ADR-0062): it needs
/// a server key, which the pure domain does not hold, so the hash arrives here as a value.
/// </remarks>
public static class EmailAddress
{
    /// <summary>Checks whether a value is shaped like an email address.</summary>
    /// <param name="value">The candidate address.</param>
    /// <returns><c>true</c> when it can be used as an address.</returns>
    /// <remarks>
    /// Deliberately minimal: the domain refuses obvious junk, and delivery is the only real test of
    /// an address. Anything stricter rejects valid addresses for no gain.
    /// </remarks>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == trimmed.LastIndexOf('@')
            && at < trimmed.Length - 1
            && !trimmed.Any(char.IsWhiteSpace);
    }

    /// <summary>Returns the form of an address that is stored and mailed.</summary>
    /// <param name="value">The address as typed.</param>
    /// <returns>The trimmed address, with its casing left alone.</returns>
    /// <remarks>
    /// Casing is preserved because the local part of an address is case-sensitive by the standard,
    /// and because <c>Anna.Meier@…</c> is how the inviter typed it and how they will recognise it in
    /// the pending list. Comparison uses the hash from <see cref="IEmailHasher"/>, which is case-insensitive.
    /// </remarks>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }
}
