// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using CircleTools.Main.Service.Core.Domain;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Mints share tokens from a cryptographic RNG (ADR-0050, NFR-009): 32 random bytes (256 bits, well
/// past the 128-bit floor) rendered as URL-safe base64 without padding, so the token drops cleanly
/// into a link fragment.
/// </summary>
public sealed class RandomShareTokenFactory : IShareTokenFactory
{
    /// <inheritdoc />
    public string Create()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
