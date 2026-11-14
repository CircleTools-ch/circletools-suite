// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Test.TestSupport;

/// <summary>
/// Test double for <see cref="IEmailHasher"/> replicating the pre-ADR-0062 unkeyed hash, so a
/// stored hash and an expected hash stay consistent across the suite.
/// </summary>
internal sealed class TestEmailHasher : IEmailHasher
{
    /// <summary>A shared instance; the hash is a pure function of the address.</summary>
    public static readonly TestEmailHasher Instance = new();

    /// <inheritdoc />
    public string Hash(string email) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
}
