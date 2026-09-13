using System.Text.Json;

namespace CircleTools.Main.Contracts;

/// <summary>
/// What a shared-by-link token grants its holder (ADR-0058, FEAT-013). The token <em>is</em> the
/// credential — the capability is bound to the token, not to any account.
/// </summary>
public enum SharedCapability
{
    /// <summary>Full organizer control: manage the roster/exclusions/budget, run the draw, manage sharing.</summary>
    Admin,

    /// <summary>Participant access: join, claim which member you are, and see your own assignment.</summary>
    Join,
}

/// <summary>
/// The two links minted when a group is shared by link (ADR-0058). The tokens are the secrets that
/// address the group on the server; the client builds the admin/join URLs around them.
/// </summary>
/// <param name="GroupId">The group's client-minted id (ADR-0008), the shared row's key.</param>
/// <param name="AdminToken">The admin-capability token.</param>
/// <param name="JoinToken">The join (participant) token.</param>
public sealed record MintedLinks(Guid GroupId, string AdminToken, string JoinToken);

/// <summary>
/// The first upload of a group into shared-by-link storage (<c>POST /api/v1/shared</c>). The group id
/// is client-minted; the body is the opaque Secret-Santa group document, stored without being
/// deserialized (ADR-0016, ADR-0033) beyond the structural abuse checks NFR-009 requires.
/// </summary>
/// <param name="GroupId">The group's client-minted id (ADR-0008).</param>
/// <param name="Version">The per-aggregate version the client holds for the group.</param>
/// <param name="Body">The group's full body as opaque JSON.</param>
public sealed record CreateSharedGroupRequest(Guid GroupId, long Version, JsonElement Body);

/// <summary>A push of a shared-by-link group's whole body (<c>PUT /api/v1/shared/{token}</c>, admin capability).</summary>
/// <param name="Version">The per-aggregate version the client holds — the client's half of last-write-wins.</param>
/// <param name="Body">The group's full body as opaque JSON.</param>
public sealed record PushSharedGroupRequest(long Version, JsonElement Body);

/// <summary>
/// A shared-by-link group as the server returns it (<c>GET /api/v1/shared/{token}</c>). The capability
/// is whatever the presented token grants.
/// </summary>
/// <param name="GroupId">The group's id.</param>
/// <param name="Version">The stored per-aggregate version.</param>
/// <param name="Body">The opaque group body.</param>
/// <param name="Capability">What the presented token grants (admin or join).</param>
/// <param name="AdminRetired">
/// True when an admin token is presented for a group an account has since claimed admin of (ADR-0058):
/// the token no longer manages the group. The join token is never retired.
/// </param>
/// <param name="AdminClaimedByName">The display name of the account that claimed admin, when known — for the retired-link message.</param>
public sealed record SharedGroupView(
    Guid GroupId,
    long Version,
    JsonElement Body,
    SharedCapability Capability,
    bool AdminRetired,
    string? AdminClaimedByName);

/// <summary>The result of pushing a shared group: the stored version and whether the push lost last-write-wins.</summary>
/// <param name="Version">The version now stored.</param>
/// <param name="Collision">Whether an older-by-server-clock body was discarded in full (ADR-0016).</param>
public sealed record PushSharedGroupResult(long Version, bool Collision);

/// <summary>The result of rotating (disabling + regenerating) a group's admin link (ADR-0058).</summary>
/// <param name="AdminToken">The freshly-minted admin token; the previous one no longer resolves.</param>
public sealed record RotatedAdminLink(string AdminToken);
