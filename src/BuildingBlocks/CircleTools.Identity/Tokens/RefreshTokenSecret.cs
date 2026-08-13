using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CircleTools.Identity.Tokens;

/// <summary>
/// Creates refresh-token secrets and the handles stored alongside them.
/// </summary>
/// <remarks>
/// The client receives the <em>secret</em>; the database only ever holds its SHA-256
/// <em>handle</em>. A dump of <c>refresh_tokens</c> therefore yields nothing usable — the same
/// reason passwords are hashed. Lookup is by handle, so this costs nothing at sign-in.
/// </remarks>
public static class RefreshTokenSecret
{
    /// <summary>Number of random bytes in a secret. 256 bits, well beyond guessing range.</summary>
    private const int SecretByteCount = 32;

    /// <summary>Mints a new secret and its stored handle.</summary>
    /// <returns>The secret to hand to the client, and the handle to persist.</returns>
    public static (string Secret, string Handle) Create()
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretByteCount));
        return (secret, HandleFor(secret));
    }

    /// <summary>Computes the stored handle for a secret presented by a client.</summary>
    /// <param name="secret">The secret the client presented.</param>
    /// <returns>The handle to look up.</returns>
    public static string HandleFor(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Base64Url.EncodeToString(hash);
    }
}
