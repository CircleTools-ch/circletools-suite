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
/// <param name="CreatorKey">
/// A high-entropy secret the sharing client mints once and keeps with the group, proving it is the
/// creator (ADR-0058 security note). This route is anonymous and the group id is <em>not</em> secret —
/// it is disclosed to every join-link holder — so the id alone must never hand back the admin token.
/// The creator key is what a lost-response retry re-presents to recover its links; a caller who knows
/// only the group id (a join-link participant) cannot, so they cannot escalate a join link to admin.
/// </param>
public sealed record CreateSharedGroupRequest(Guid GroupId, long Version, JsonElement Body, string CreatorKey);

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
/// <param name="JoinToken">
/// The group's current join token — filled only on the owner read (<c>GET /shared/{groupId}/owned</c>), so
/// the owner can share the join link from any of their devices (ADR-0078 Amendment 3). Never returned to a
/// token holder. Additive: <see langword="null"/> from an older server.
/// </param>
/// <param name="ClaimedMemberId">
/// On a join read, the member the request proved to be (participant key or account, ADR-0080);
/// <see langword="null"/> when it proved none. Additive.
/// </param>
/// <param name="Redacted">
/// True when <paramref name="Body"/> was trimmed for a join-link reader (ADR-0080): it then holds only the
/// proven member's pairing and their own and their recipient's wishlist and gift note. Additive.
/// </param>
/// <param name="TakenMembers">
/// On a join read, the members that can't be claimed and why (ADR-0080, Q6). Additive: <see langword="null"/>
/// from an older server and on admin/owner reads.
/// </param>
public sealed record SharedGroupView(
    Guid GroupId,
    long Version,
    JsonElement Body,
    SharedCapability Capability,
    bool AdminRetired,
    string? AdminClaimedByName,
    string? JoinToken = null,
    Guid? ClaimedMemberId = null,
    bool Redacted = false,
    IReadOnlyList<TakenMember>? TakenMembers = null);

/// <summary>Why a member of a shared group can't be claimed through the join link (ADR-0080).</summary>
public enum TakenReason
{
    /// <summary>Someone already joined as this member.</summary>
    Joined,

    /// <summary>The member is linked to an account.</summary>
    Linked,

    /// <summary>The member is the organizer (linked to the owning account).</summary>
    Organizer,
}

/// <summary>A member that can't be claimed through the join link, with the reason (ADR-0080).</summary>
/// <param name="MemberId">The member.</param>
/// <param name="Reason">Why it is taken.</param>
public sealed record TakenMember(Guid MemberId, TakenReason Reason);

/// <summary>The result of pushing a shared group: the stored version and whether the push lost last-write-wins.</summary>
/// <param name="Version">The version now stored.</param>
/// <param name="Collision">Whether an older-by-server-clock body was discarded in full (ADR-0016).</param>
/// <param name="Rewritten">
/// Whether the server changed the pushed body before storing it — it re-applied participants' own entries
/// (ADR-0080) — so the pusher must pull the stored version. Additive: <see langword="false"/> from an older server.
/// </param>
public sealed record PushSharedGroupResult(long Version, bool Collision, bool Rewritten = false);

/// <summary>The result of rotating (disabling + regenerating) a group's admin link (ADR-0058).</summary>
/// <param name="AdminToken">The freshly-minted admin token; the previous one no longer resolves.</param>
public sealed record RotatedAdminLink(string AdminToken);

/// <summary>How a person-claim on a shared-by-link group is remembered (ADR-0058, FEAT-013).</summary>
public enum PersonClaimKind
{
    /// <summary>The claim lives on one device only — no account behind it.</summary>
    Device,

    /// <summary>The claim is bound to a signed-in account, durable across that user's devices.</summary>
    Account,
}

/// <summary>The set of members already claimed in a shared-by-link group (ADR-0058) — so the join UI can lock them.</summary>
/// <param name="MemberIds">The claimed member ids.</param>
/// <param name="SelfMaintained">
/// The members who keep their own entry (a participant entry exists — a joined participant or a reveal-link
/// holder, ADR-0080/ADR-0084), so the organizer's copy of their wishlist and gift note is read-only.
/// <see langword="null"/> from an older server.
/// </param>
public sealed record ClaimedMembers(IReadOnlyList<Guid> MemberIds, IReadOnlyList<Guid>? SelfMaintained = null);

/// <summary>A person-claim through the join link (ADR-0058): "this is me".</summary>
/// <param name="MemberId">The member the joiner is claiming.</param>
/// <param name="Kind">Whether the claim is device-only or bound to an account.</param>
/// <param name="DeviceHandle">The claiming device's stable handle, for a device claim.</param>
/// <param name="AccountId">
/// Ignored by the server since ADR-0080: an account claim is bound to the signed-in caller (the bearer),
/// never to an id the client names. Kept for wire compatibility.
/// </param>
public sealed record ClaimPersonRequest(Guid MemberId, PersonClaimKind Kind, string? DeviceHandle, Guid? AccountId);

/// <summary>The outcome of a person-claim (ADR-0058): claimed, or already taken (first-claim-locks).</summary>
/// <param name="Claimed">Whether this call claimed the member.</param>
/// <param name="AlreadyClaimed">Whether the member was already claimed by someone else (the lock held).</param>
/// <param name="ParticipantKey">
/// On a successful claim, the secret that proves this claim on later requests (ADR-0080); the server keeps
/// only its hash. Additive: <see langword="null"/> from an older server.
/// </param>
public sealed record PersonClaimResponse(bool Claimed, bool AlreadyClaimed, string? ParticipantKey = null);

/// <summary>One event's wishlist in a participant's own entry (ADR-0080).</summary>
/// <param name="EventId">The event.</param>
/// <param name="Items">The wishlist items, in order; empty clears the wishlist.</param>
public sealed record ParticipantWishlist(Guid EventId, IReadOnlyList<string> Items);

/// <summary>
/// What a joined participant maintains themselves (ADR-0080): their gift note and their wishlist per event.
/// A write replaces the whole entry.
/// </summary>
/// <param name="GiftNote">The participant's gift note; null or blank clears it.</param>
/// <param name="Wishlists">The participant's wishlist for each event they set one for.</param>
public sealed record ParticipantEntry(string? GiftNote, IReadOnlyList<ParticipantWishlist> Wishlists);

/// <summary>The result of writing a participant's own entry: the group version now stored.</summary>
/// <param name="Version">The stored version.</param>
public sealed record ParticipantEntryResult(long Version);

/// <summary>Exchanges a pre-ADR-0080 device claim's device handle for a participant key, once.</summary>
/// <param name="DeviceHandle">The handle the device claimed with.</param>
public sealed record ParticipantKeyRequest(string DeviceHandle);

/// <summary>A participant key issued for an existing claim (ADR-0080).</summary>
/// <param name="ParticipantKey">The key; the server keeps only its hash.</param>
public sealed record ParticipantKeyResponse(string ParticipantKey);

/// <summary>A shared group the signed-in account joined as a member (ADR-0080).</summary>
/// <param name="GroupId">The group.</param>
/// <param name="JoinToken">The group's current join token, to read it from this device.</param>
/// <param name="MemberId">The member the account claimed.</param>
public sealed record JoinedSharedGroup(Guid GroupId, string JoinToken, Guid MemberId);

/// <summary>The shared groups the signed-in account joined (ADR-0080).</summary>
/// <param name="Groups">The joined groups.</param>
public sealed record JoinedSharedGroups(IReadOnlyList<JoinedSharedGroup> Groups);

/// <summary>One group the calling account owns in shared-by-link storage (ADR-0058, D2 discovery).</summary>
/// <param name="GroupId">The group's id.</param>
/// <param name="Version">The stored version.</param>
/// <param name="Body">The opaque group body.</param>
public sealed record OwnedSharedGroup(Guid GroupId, long Version, JsonElement Body);

/// <summary>The groups the calling account owns, so a second device can discover and manage them (D2).</summary>
/// <param name="Groups">The owned groups.</param>
public sealed record OwnedSharedGroups(IReadOnlyList<OwnedSharedGroup> Groups);

/// <summary>
/// One giver's reveal slice (ADR-0068, FEAT-020): the per-giver reveal payload a draw-capable client
/// publishes so a backend-backed group's reveal link can show the current wishlist and event details.
/// The <see cref="Body"/> is opaque to the server (like the group body) and addressed by its own
/// unguessable reveal token — one token grants exactly this one giver's slice, never the draw (NFR-002).
/// </summary>
/// <param name="RevealToken">The per-giver reveal token this slice is addressed by (the link's <c>rt=</c>).</param>
/// <param name="Body">The encoded reveal payload as opaque JSON (the same shape the offline snapshot carries).</param>
/// <param name="Key">
/// A stable, opaque key for the slice's place in the draw (the client sends "event:giver"). When a slice with
/// the same key is already stored, the server keeps <em>its</em> token and returns it, so every organizer
/// device ends up handing out the same link (ADR-0068 amendment, 2026-10-16). <see langword="null"/> from
/// clients that predate it: the token sent is stored as before.
/// </param>
/// <param name="EventId">
/// The event the slice belongs to, so the token holder can keep their own wishlist for it (ADR-0084).
/// <see langword="null"/> from clients that predate it: no own-entry editor for that slice.
/// </param>
/// <param name="MemberId">
/// The giver the slice is for — the token holder (ADR-0084). Names only the giver; the recipient stays inside
/// the opaque <see cref="Body"/> (NFR-002). <see langword="null"/> from clients that predate it.
/// </param>
public sealed record RevealSlice(string RevealToken, JsonElement Body, string? Key = null, Guid? EventId = null, Guid? MemberId = null);

/// <summary>The reveal token in effect for a keyed slice after a publish (ADR-0068 amendment).</summary>
/// <param name="Key">The slice's key as sent.</param>
/// <param name="RevealToken">The token the slice is stored under — an existing one when the key was known.</param>
public sealed record RevealTokenForKey(string Key, string RevealToken);

/// <summary>The answer to a reveal-slice publish: the tokens in effect for the keyed slices.</summary>
/// <param name="Tokens">One entry per keyed slice.</param>
public sealed record PublishRevealSlicesResponse(IReadOnlyList<RevealTokenForKey> Tokens);

/// <summary>
/// Publishes the full current set of reveal slices for a group (ADR-0068, FEAT-020):
/// <c>PUT /api/v1/shared/{adminToken}/reveal-slices</c> (admin token) or
/// <c>PUT /api/v1/shared/{groupId}/owned/reveal-slices</c> (owner bearer). The set is authoritative —
/// the server upserts these and removes any of the group's slices not present, so a re-draw's stale
/// reveal tokens stop resolving.
/// </summary>
/// <param name="Slices">The complete set of per-giver slices for the group's current draw.</param>
public sealed record PublishRevealSlicesRequest(IReadOnlyList<RevealSlice> Slices);

/// <summary>
/// A reveal slice as the server returns it to the public reveal page (ADR-0068, FEAT-020):
/// <c>GET /api/v1/shared/reveal/{revealToken}</c>. Body only — the token holder learns their own slice
/// and nothing else about the group.
/// </summary>
/// <param name="Body">The opaque reveal payload for this giver.</param>
public sealed record RevealSliceView(JsonElement Body);

/// <summary>
/// The reveal-link holder's own entry as the reveal page shows it for editing (ADR-0084, FR-050):
/// <c>GET /api/v1/shared/reveal/{revealToken}/me</c>.
/// </summary>
/// <param name="GiftNote">The holder's gift note (group-wide), or <see langword="null"/> when unset.</param>
/// <param name="Items">The holder's wishlist for the reveal token's event (may be empty).</param>
public sealed record RevealEntryView(string? GiftNote, IReadOnlyList<string> Items);

/// <summary>
/// Replaces the reveal-link holder's own gift note and their wishlist for the reveal token's event
/// (ADR-0084, FR-050): <c>PUT /api/v1/shared/reveal/{revealToken}/me</c>. Their wishlists for other events
/// are kept.
/// </summary>
/// <param name="GiftNote">The gift note; null or blank clears it.</param>
/// <param name="Items">The wishlist items for the token's event; empty clears it.</param>
public sealed record RevealEntryWrite(string? GiftNote, IReadOnlyList<string> Items);
