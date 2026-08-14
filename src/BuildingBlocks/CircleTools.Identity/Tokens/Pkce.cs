using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CircleTools.Identity.Tokens;

/// <summary>
/// PKCE (RFC 7636) helpers for the cross-app SSO hand-off (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The authorization code travels in a URL, so possession of the code must not be enough to redeem
/// it. The requesting app keeps a random <em>verifier</em> in memory and sends only its
/// <em>challenge</em> (the base64url SHA-256 of the verifier) when the code is minted; it presents the
/// verifier when it redeems. A code skimmed from the URL is useless without the verifier. Only the
/// <c>S256</c> method is supported — <c>plain</c> would defeat the point.
/// </remarks>
public static class Pkce
{
    /// <summary>The only supported challenge method.</summary>
    public const string MethodS256 = "S256";

    /// <summary>Computes the <c>S256</c> challenge for a verifier.</summary>
    /// <param name="codeVerifier">The verifier the client generated.</param>
    /// <returns>The base64url-encoded SHA-256 of the verifier's ASCII bytes.</returns>
    public static string ChallengeFor(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64Url.EncodeToString(hash);
    }

    /// <summary>
    /// Verifies a presented verifier against a stored challenge in constant time.
    /// </summary>
    /// <param name="codeVerifier">The verifier the client presented at redemption.</param>
    /// <param name="codeChallenge">The challenge stored when the code was minted.</param>
    /// <param name="method">The stored challenge method; anything but <c>S256</c> is rejected.</param>
    /// <returns><see langword="true"/> only if the verifier matches the challenge.</returns>
    public static bool Verify(string? codeVerifier, string? codeChallenge, string? method)
    {
        if (!string.Equals(method, MethodS256, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(codeChallenge))
        {
            return false;
        }

        var computed = ChallengeFor(codeVerifier);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(codeChallenge));
    }
}
