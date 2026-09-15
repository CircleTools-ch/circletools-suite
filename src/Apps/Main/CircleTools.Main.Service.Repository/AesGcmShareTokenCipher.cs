using System.Security.Cryptography;
using System.Text;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// AES-256-GCM implementation of <see cref="IShareTokenCipher"/> (ADR-0061). Each token is sealed under a
/// fresh random nonce with an authentication tag, and the stored value packs <c>nonce || tag ||
/// ciphertext</c> as Base64. Authenticated encryption means a tampered stored value fails to decrypt
/// rather than yielding a forged token.
/// </summary>
public sealed class AesGcmShareTokenCipher : IShareTokenCipher
{
    private const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    private const int KeySize = 32;   // AES-256

    private readonly byte[] _key;

    /// <summary>Creates the cipher from the configured key, failing fast when it is absent or malformed.</summary>
    /// <param name="options">The shared-group security options carrying the Base64 AES-256 key.</param>
    /// <exception cref="InvalidOperationException">The key is missing or not a 32-byte Base64 value.</exception>
    public AesGcmShareTokenCipher(IOptions<SharedGroupSecurityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configured = options.Value.TokenEncryptionKey;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"'{SharedGroupSecurityOptions.SectionName}:{nameof(SharedGroupSecurityOptions.TokenEncryptionKey)}' is not configured.");
        }

        try
        {
            _key = Convert.FromBase64String(configured);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The share-token encryption key is not valid Base64.", ex);
        }

        if (_key.Length != KeySize)
        {
            throw new InvalidOperationException($"The share-token encryption key must be {KeySize} bytes (AES-256).");
        }
    }

    /// <inheritdoc />
    public string Encrypt(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var plaintext = Encoding.UTF8.GetBytes(token);
        var packed = new byte[NonceSize + TagSize + plaintext.Length];
        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        var ciphertext = packed.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Convert.ToBase64String(packed);
    }

    /// <inheritdoc />
    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var packed = Convert.FromBase64String(ciphertext);
        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        var sealed_ = packed.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[sealed_.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, sealed_, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
