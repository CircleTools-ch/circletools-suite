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
/// One giver's reveal slice for a backend-backed group (ADR-0068, FEAT-020): the per-giver reveal
/// payload a draw-capable client published, stored opaquely and addressed by an unguessable reveal
/// token — exactly as opaque to the server as <see cref="SharedGroupRow.Body"/>. The public reveal page
/// GETs one of these by token to show the current wishlist/event; possession of the token grants this
/// one giver's slice and nothing else (NFR-002).
/// </summary>
public sealed class SharedRevealSlice
{
    /// <summary>
    /// The <em>hash</em> of the reveal token — the row key (ADR-0061). The token from a link is looked up
    /// by the SHA-256 of itself, so a database dump holds no working reveal tokens.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The reveal token's plaintext, encrypted at rest (ADR-0061), so the sharing client can hand back its stable link.</summary>
    public string TokenCipher { get; set; } = string.Empty;

    /// <summary>The group this slice belongs to — used to replace a group's whole slice set on re-publish.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The client's stable key for this slice ("event:giver"), so a re-publish keeps the token (ADR-0068 amendment).</summary>
    public string? Key { get; set; }

    /// <summary>
    /// The event the slice belongs to (ADR-0084): the token holder may keep their own wishlist for it.
    /// <see langword="null"/> for a slice published by a client that predates it.
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// The giver the slice is for — the token holder (ADR-0084). Only the giver: the recipient stays inside the
    /// opaque <see cref="Body"/> (NFR-002). <see langword="null"/> for a slice from an older client.
    /// </summary>
    public Guid? MemberId { get; set; }

    /// <summary>The giver's opaque reveal payload as JSON (stored as <c>jsonb</c>, never deserialized by the server).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>When the slice was last published — drives the retention sweep alongside the group (NFR-010).</summary>
    public DateTimeOffset LastActivityAt { get; set; }
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

    /// <summary>
    /// The hash of the participant key that proves this claim on later requests (ADR-0080), or
    /// <see langword="null"/> for a claim made before keys existed and not yet exchanged. Only the hash is
    /// stored, as for share tokens (ADR-0061).
    /// </summary>
    public string? ParticipantKeyHash { get; set; }
}

/// <summary>
/// A joined participant's own entry in a shared-by-link group (ADR-0080): their gift note and their
/// wishlist per event. Merged into the group body on write and re-applied over every admin/owner push, so a
/// stale organizer copy can't undo it. Removed with the claim.
/// </summary>
public sealed class SharedMemberEntry
{
    /// <summary>The group.</summary>
    public Guid GroupId { get; set; }

    /// <summary>The claimed member the entry belongs to.</summary>
    public Guid MemberId { get; set; }

    /// <summary>The participant's gift note, or <see langword="null"/> when unset.</summary>
    public string? GiftNote { get; set; }

    /// <summary>The participant's per-event wishlists, as JSON (<c>[{ eventId, items }]</c>).</summary>
    public string Wishlists { get; set; } = "[]";

    /// <summary>When the entry was last written.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
