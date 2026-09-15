using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// A shared-by-link group as the store keeps it (ADR-0058, FEAT-013): a versioned, opaque JSON
/// document keyed by the group's own client-minted id and addressed by share tokens rather than by an
/// account. <see cref="OwnerAccountId"/> is null while the group is account-free and is set when an
/// account claims admin; the row stays physically here either way so join-link holders never lose
/// access.
/// </summary>
public sealed class SharedGroupRow
{
    /// <summary>The group's client-minted id (ADR-0008) — the row key.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The group's full body as opaque JSON (stored as <c>jsonb</c>).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The per-aggregate version the client bumps on each edit — the client's half of last-write-wins.</summary>
    public long Version { get; set; }

    /// <summary>The server-clock instant this body was last stored — the last-write-wins ordering key.</summary>
    public DateTimeOffset ServerModifiedAt { get; set; }

    /// <summary>
    /// The <em>hash</em> of the creator's secret (ADR-0058 security note, ADR-0061): the high-entropy key
    /// the sharing client presented on the first upload, stored only as its SHA-256 so a database dump
    /// never yields the key itself. Only a re-create that re-presents a key hashing to this value may
    /// recover the group's admin/join links — the group id alone (which every join-link holder learns)
    /// must not. Null only for rows created before the key existed.
    /// </summary>
    public string? CreatorKey { get; set; }

    /// <summary>The account that has claimed admin, or null while the group is account-free.</summary>
    public Guid? OwnerAccountId { get; set; }

    /// <summary>The display name of the account that claimed admin, for the retired-admin-link message.</summary>
    public string? AdminClaimedByName { get; set; }

    /// <summary>When the group was first shared.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The last time the group was read or written — drives the retention sweep (NFR-010).</summary>
    public DateTimeOffset LastActivityAt { get; set; }
}

/// <summary>
/// A share token addressing a <see cref="SharedGroupRow"/> and granting a capability (ADR-0058). The
/// token is the credential; several people may hold the same admin token at once. Rotating deactivates
/// the old token; claiming admin retires the admin token (<see cref="RetiredByClaim"/>) so a holder is
/// told who took over rather than seeing an indistinguishable not-found.
/// </summary>
public sealed class SharedGroupToken
{
    /// <summary>
    /// The <em>hash</em> of the token — the row key (ADR-0061). The token carried in a link is looked up
    /// by the SHA-256 of itself, so the stored row never holds the token in the clear and a database dump
    /// yields no working links.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The token's plaintext, encrypted at rest (ADR-0061). Kept only so the resume-share path (ADR-0058)
    /// can hand the original working link back to its creator; a dump without the key cannot read it.
    /// </summary>
    public string TokenCipher { get; set; } = string.Empty;

    /// <summary>The group this token addresses.</summary>
    public Guid GroupId { get; set; }

    /// <summary>What the token grants.</summary>
    public SharedCapability Capability { get; set; }

    /// <summary>Whether the token still resolves. A rotated or deleted token is inactive.</summary>
    public bool Active { get; set; }

    /// <summary>
    /// True when an admin token was deactivated because an account claimed admin (as opposed to a
    /// rotation): the store returns a retired view for it so the holder learns who took over.
    /// </summary>
    public bool RetiredByClaim { get; set; }

    /// <summary>When the token was minted.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A person-claim on a shared-by-link group (ADR-0058, FEAT-013): the record that a member has been
/// claimed by a device or an account, locking that member so no one else can claim it. Only an admin
/// can release it (delete the row).
/// </summary>
public sealed class SharedMemberClaim
{
    /// <summary>The group the claimed member belongs to.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The claimed member's id (client-minted, from the group body).</summary>
    public Guid MemberId { get; set; }

    /// <summary>Whether the claim is held by a device or an account.</summary>
    public PersonClaimKind ClaimKind { get; set; }

    /// <summary>The device handle holding the claim, for a <see cref="PersonClaimKind.Device"/> claim.</summary>
    public string? DeviceHandle { get; set; }

    /// <summary>The account holding the claim, for a <see cref="PersonClaimKind.Account"/> claim.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>When the member was claimed.</summary>
    public DateTimeOffset ClaimedAt { get; set; }
}
