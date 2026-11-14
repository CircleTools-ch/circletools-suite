// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CircleTools.Circles.Core.Domain;
using Microsoft.Extensions.Options;

namespace CircleTools.Circles.Repository;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="IEmailHasher"/> (ADR-0062). The address is canonicalised the
/// way <see cref="EmailAddress"/> compares — trimmed and upper-invariant — then keyed-hashed, so the same
/// address always yields the same value and a database dump on its own cannot be dictionary-attacked.
/// </summary>
public sealed class HmacEmailHasher : IEmailHasher
{
    private readonly byte[] _key;

    /// <summary>Creates the hasher from the configured key, failing fast when it is absent or malformed.</summary>
    /// <param name="options">The circle security options carrying the Base64 HMAC key.</param>
    /// <exception cref="InvalidOperationException">The key is missing or not valid Base64.</exception>
    public HmacEmailHasher(IOptions<CircleSecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.EmailHashKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"'{CircleSecurityOptions.SectionName}:{nameof(CircleSecurityOptions.EmailHashKey)}' is not configured.");
        }

        try
        {
            _key = Convert.FromBase64String(configured);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The email-hash key is not valid Base64.", ex);
        }
    }

    /// <inheritdoc />
    public string Hash(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var canonical = email.Trim().ToUpperInvariant();
        return Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonical)));
    }
}
