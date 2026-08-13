using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// Creates the secret that goes into an invitation link, and the handle stored alongside it.
/// </summary>
/// <remarks>
/// Same rule as refresh tokens: the recipient's mail carries the <em>secret</em>, the database only
/// ever holds its SHA-256 <em>handle</em>. A dump of the invitations table therefore yields no
/// usable links. Lookup is by handle, so this costs nothing when a link is opened.
/// <para>
/// The secret is not a bearer key to the circle — holding it lets someone read the invitation and
/// decline it, never join (FR-018). Joining is bound to a verified address.
/// </para>
/// </remarks>
public static class InvitationSecret
{
    /// <summary>Number of random bytes in a secret. 256 bits, well beyond guessing range.</summary>
    private const int SecretByteCount = 32;

    /// <summary>Mints a new secret and its stored handle.</summary>
    /// <returns>The secret to put in the mailed link, and the handle to persist.</returns>
    public static (string Secret, string Handle) Create()
    {
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretByteCount));
        return (secret, HandleFor(secret));
    }

    /// <summary>Computes the stored handle for a secret presented by a recipient.</summary>
    /// <param name="secret">The secret from the link.</param>
    /// <returns>The handle to look up.</returns>
    public static string HandleFor(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
