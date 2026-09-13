namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// Mints unguessable share tokens for shared-by-link groups (ADR-0058, NFR-009). Behind a port so the
/// entropy is unit-testable and the one cryptographic dependency is explicit (ADR-0050).
/// </summary>
public interface IShareTokenFactory
{
    /// <summary>Creates a fresh, URL-safe token with at least 128 bits of entropy.</summary>
    /// <returns>A new token, distinct from any previously minted with overwhelming probability.</returns>
    string Create();
}
