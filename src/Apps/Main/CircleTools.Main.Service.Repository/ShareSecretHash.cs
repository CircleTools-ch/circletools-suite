// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// The deterministic lookup hash stored in place of a high-entropy share secret — a share token or a
/// creator key (ADR-0061). SHA-256, Base64Url-encoded, no salt: the input already carries 256 bits of
/// entropy, so there is no dictionary to defend against, and a salt would only break the deterministic
/// lookup the token's primary key depends on.
/// </summary>
internal static class ShareSecretHash
{
    /// <summary>Hashes a share secret to its stored lookup form.</summary>
    /// <param name="secret">The high-entropy token or key, as presented by the client.</param>
    /// <returns>The Base64Url-encoded SHA-256 of the secret.</returns>
    public static string Of(string secret) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
