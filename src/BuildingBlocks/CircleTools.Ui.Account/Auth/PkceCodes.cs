using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace CircleTools.Ui.Account.Auth;

/// <summary>
/// Generates a PKCE verifier/challenge pair for the SSO hand-off (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The verifier is a high-entropy random string the requesting app keeps to itself; the challenge is
/// its base64url SHA-256, safe to send in the redirect. The server verifies the pair with the same
/// <c>S256</c> rule. This mirrors the server's <c>Pkce</c> helper deliberately — the two ends must
/// agree byte for byte — but the block must not reference the server, so the small computation lives
/// on both sides rather than in a shared type.
/// </remarks>
public static class PkceCodes
{
    /// <summary>The PKCE challenge method this flow uses.</summary>
    public const string Method = "S256";

    /// <summary>32 random bytes — 256 bits of entropy in the verifier.</summary>
    private const int VerifierByteCount = 32;

    /// <summary>Creates a fresh verifier and its matching challenge.</summary>
    /// <returns>The verifier to keep, and the challenge to send.</returns>
    public static (string Verifier, string Challenge) Create()
    {
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(VerifierByteCount));
        var challenge = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }
}
