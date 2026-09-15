namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// Protects a share token at rest (ADR-0061). A token is looked up by a deterministic hash of itself, so
/// the stored row never holds the token in the clear; its plaintext is kept only as reversible ciphertext
/// so the resume-share path (ADR-0058) can still hand the original working link back to its creator.
/// Behind a port so the key dependency is explicit (ADR-0050) and the encrypt/decrypt round-trip is
/// unit-testable.
/// </summary>
public interface IShareTokenCipher
{
    /// <summary>Encrypts a freshly minted share token for storage.</summary>
    /// <param name="token">The plaintext token handed to the client.</param>
    /// <returns>Opaque ciphertext to persist alongside the token's lookup hash.</returns>
    string Encrypt(string token);

    /// <summary>Recovers a share token's plaintext from its stored ciphertext.</summary>
    /// <param name="ciphertext">The value produced by <see cref="Encrypt"/>.</param>
    /// <returns>The original token, so a resumed share returns the same link (ADR-0058).</returns>
    string Decrypt(string ciphertext);
}
