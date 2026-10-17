using System.Text.Json;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// The server side of the account-free, shared-by-link storage tier (ADR-0058, FEAT-013): a group
/// stored on the server keyed by an unguessable <em>share token</em> rather than by an account, and
/// reached by an admin or join link.
/// </summary>
/// <remarks>
/// Like <see cref="ISyncStore"/> this is Platform: the group body is an opaque, versioned JSON
/// document the store keeps and returns without understanding the Secret-Santa schema. What differs is
/// authorization — the token, not a bearer identity, is the credential — and the presence of an
/// optional owning account (set when someone claims admin). Structural abuse limits (NFR-009) are the
/// only inspection the store makes of the body.
/// </remarks>
public interface ISharedGroupStore
{
    /// <summary>Uploads a group into shared-by-link storage and mints its admin + join tokens.</summary>
    /// <param name="request">The group id, version, opaque body and the creator key (ADR-0058).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The outcome: <see cref="CreateSharedGroupStatus.Created"/> with the links on a first share;
    /// <see cref="CreateSharedGroupStatus.TooLarge"/> when the body exceeds the NFR-009 abuse limits; or
    /// <see cref="CreateSharedGroupStatus.Refused"/>. Re-creating a group already shared returns its
    /// current active links <em>only</em> when the request carries the matching creator key (an
    /// idempotent, lost-response-safe retry by the original creator) — a caller who knows just the group
    /// id, or who omits the key, or who targets an account-owned group, is refused, so a join-link
    /// holder cannot escalate to admin.
    /// </returns>
    Task<CreateSharedGroupResult> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a token to the group it addresses and the capability it grants. A join-token read is
    /// trimmed to the member <paramref name="proof"/> proves (ADR-0080); admin reads are full.
    /// </summary>
    /// <param name="token">The admin or join token from a link.</param>
    /// <param name="proof">What the reader presented to prove a claimed member (participant key or account).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The group view, or <see langword="null"/> when the token does not resolve — a wrong, disabled,
    /// or never-existed token are indistinguishable (no oracle). A retired admin token resolves to a
    /// view with <see cref="SharedGroupView.AdminRetired"/> set, so the client can explain it.
    /// </returns>
    Task<SharedGroupView?> GetAsync(string token, ParticipantProof proof, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for the group an admin token addresses (last-write-wins by server clock).</summary>
    /// <param name="token">The token; must grant the admin capability and not be retired.</param>
    /// <param name="version">The version the client holds — its half of last-write-wins.</param>
    /// <param name="body">The new opaque body.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write outcome (applied / collision / not-found / forbidden / too-large).</returns>
    Task<SharedWriteResult> PushAsync(string token, long version, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>Disables the current admin token for a group and mints a fresh one (the previous stops resolving).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The new admin token, or <see langword="null"/> when the token does not grant admin.</returns>
    Task<RotatedAdminLink?> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Deletes the online copy of a group and all its tokens (owner-less delete, NFR-010).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when a group was deleted; <see langword="false"/> when the token does not grant admin.</returns>
    Task<bool> DeleteAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Reads which members of a shared group are already claimed, so the join UI can lock them.</summary>
    /// <param name="token">Any active token for the group (join or admin).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The claimed member ids, or <see langword="null"/> when the token does not resolve.</returns>
    Task<IReadOnlyList<Guid>?> GetClaimedMembersAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads which members keep their own entry — a joined participant's or a reveal-link holder's
    /// (ADR-0080/ADR-0084) — so the organizer's copy of their wishlist and gift note is read-only.
    /// </summary>
    /// <param name="token">Any active token for the group (join or admin).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The member ids with an entry; empty when the token does not resolve.</returns>
    Task<IReadOnlyList<Guid>> GetSelfMaintainedMembersAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Claims a member for the caller through the join link — first claim wins and locks (ADR-0058).</summary>
    /// <param name="token">An active join or admin token for the group.</param>
    /// <param name="request">Which member, and how the claim is remembered (device or account).</param>
    /// <param name="callerAccountId">
    /// The signed-in caller, if any. An account claim binds to this account only (ADR-0080); without a
    /// caller an account claim is recorded as a device claim.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The claim outcome (claimed / already-claimed / not-found).</returns>
    Task<PersonClaimResult> ClaimPersonAsync(string token, ClaimPersonRequest request, Guid? callerAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a participant key for a device claim made before keys existed (ADR-0080), once: only when the
    /// presented device handle matches the claim's and the claim has no key yet.
    /// </summary>
    /// <param name="token">An active join token for the group.</param>
    /// <param name="memberId">The claimed member.</param>
    /// <param name="deviceHandle">The handle the device claimed with.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The new key, or <see langword="null"/> when refused.</returns>
    Task<string?> IssueParticipantKeyAsync(string token, Guid memberId, string deviceHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a joined participant's own entry — gift note and per-event wishlists (ADR-0080): records it,
    /// merges it into the group body and bumps the version.
    /// </summary>
    /// <param name="token">An active join token for the group.</param>
    /// <param name="proof">The participant's proof of their claimed member.</param>
    /// <param name="entry">The entry; replaces the previous one.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>Applied with the new version; Forbidden without a proven claim; TooLarge over the limits; NotFound for a bad token.</returns>
    Task<SharedWriteResult> WriteEntryAsync(string token, ParticipantProof proof, ParticipantEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Releases the participant's own claim and entry — leaving the group (ADR-0080, Q1).</summary>
    /// <param name="token">An active join token for the group.</param>
    /// <param name="proof">The participant's proof of their claimed member.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when a proven claim was released.</returns>
    Task<bool> LeaveAsync(string token, ParticipantProof proof, CancellationToken cancellationToken = default);

    /// <summary>Lists the shared groups the account claimed a member in, with their join tokens (ADR-0080).</summary>
    /// <param name="accountId">The signed-in account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The joined groups.</returns>
    Task<IReadOnlyList<JoinedSharedGroup>> ListJoinedAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Releases every claim an account holds, with its entries — account deletion (ADR-0080).</summary>
    /// <param name="accountId">The departing account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>How many claims were released.</returns>
    Task<int> ReleaseClaimsOfAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the store knows about who organizes a group, for authorizing the linking publish (ADR-0080):
    /// its owner account and whether the presented admin token is active for it.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="adminToken">An admin token the caller presented, if any.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The facts, or <see langword="null"/> when the store holds no such group.</returns>
    Task<SharedGroupOrganizer?> GetOrganizerAsync(Guid groupId, string? adminToken, CancellationToken cancellationToken = default);

    /// <summary>Releases a claimed member so it can be claimed again — admin only, the sole recovery path (ADR-0058).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the member was released (or was already free); <see langword="false"/> when the token does not grant admin.</returns>
    Task<bool> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a claimed member of a group the account owns (ADR-0081) — the owner's counterpart of
    /// <see cref="ReleasePersonAsync"/>, since an owned group has no admin token. The member's entry goes too.
    /// </summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the caller owns the group (idempotent for a free member).</returns>
    Task<bool> ReleasePersonAsOwnerAsync(Guid accountId, Guid groupId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims admin of a shared-by-link group into an account (Shared by link → My account, ADR-0058):
    /// the account becomes the sole admin, the admin link is retired, and the join link keeps working.
    /// First claim wins — a group already owned cannot be claimed again.
    /// </summary>
    /// <param name="adminToken">The admin token being claimed from.</param>
    /// <param name="accountId">The signed-in account taking ownership.</param>
    /// <param name="displayName">The owner's display name, for the retired-admin-link message.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the account now owns the group; <see langword="false"/> when the token was not an active admin token or the group is already owned.</returns>
    Task<bool> ClaimAdminAsync(string adminToken, Guid accountId, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an account-owned group back to a shared-by-link group (My account → Shared by link,
    /// ADR-0058): clears ownership and mints a fresh admin link, so ownership is not a trap.
    /// </summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="groupId">The group to release.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The freshly-minted admin link, or <see langword="null"/> when the caller is not the owner.</returns>
    Task<RotatedAdminLink?> ReleaseAdminAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the online copy of an account-owned group and all its tokens (My account, ADR-0078): the owner
    /// has no admin token, so the group is addressed by id. Like the admin-link delete, the join link stops
    /// working too. Idempotent: a group that is already gone is a success, so a retried delete settles.
    /// </summary>
    /// <param name="accountId">The calling account.</param>
    /// <param name="groupId">The group to delete.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the group is gone (deleted now or already absent); <see langword="false"/> when it exists but the caller does not own it.</returns>
    Task<bool> DeleteOwnedAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Deletes owner-less shared groups whose last activity predates <paramref name="olderThan"/> (retention, NFR-010).</summary>
    /// <param name="olderThan">The cutoff; groups inactive since before this are removed.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of groups removed.</returns>
    Task<int> PruneExpiredAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);

    /// <summary>Lists the groups an account owns, so a second device can discover and manage them (D2 discovery).</summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The owned groups (id, version, body).</returns>
    Task<IReadOnlyList<OwnedSharedGroup>> ListOwnedAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Reads a group the caller owns (My account mode, D2): the owner reads by group id + bearer, having no admin token.</summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The group view (admin capability), or <see langword="null"/> when the caller is not the owner.</returns>
    Task<SharedGroupView?> GetAsOwnerAsync(Guid accountId, Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a shared group still exists, whoever owns it. Lets the owner read tell a deleted group (gone)
    /// from one the caller no longer owns, so the account's other devices can drop or hand back their copy
    /// (ADR-0078).
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the group exists.</returns>
    Task<bool> ExistsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for a group the caller owns (My account mode, D2): last-write-wins, owner-authorized.</summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="version">The version the client holds — its half of last-write-wins.</param>
    /// <param name="body">The new opaque body.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write outcome (applied / collision / not-found / forbidden / too-large).</returns>
    Task<SharedWriteResult> PushAsOwnerAsync(Guid accountId, Guid groupId, long version, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hands every group an account owns back to shared-by-link on account deletion (FR-043, the
    /// default disposition, ADR-0058): clears ownership so the group stays online and its join-link
    /// participants keep access — nobody is stranded. Each group's local copy on any device is untouched.
    /// </summary>
    /// <param name="accountId">The departing account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of groups handed off.</returns>
    Task<int> ReleaseAllOwnedAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every group an account owns, online copies and all (FR-043, the "delete them too"
    /// disposition an owner may choose at account deletion instead of the hand-off default).
    /// </summary>
    /// <param name="accountId">The departing account.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of groups deleted.</returns>
    Task<int> DeleteAllOwnedAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the full current set of per-giver reveal slices for the group an admin token addresses
    /// (ADR-0068, FEAT-020). The set is authoritative: the store replaces the group's slices with it, so a
    /// re-draw's stale reveal tokens stop resolving. Each slice body is opaque and abuse-capped like the
    /// group body (NFR-009).
    /// </summary>
    /// <param name="adminToken">The current admin token for the group.</param>
    /// <param name="slices">The complete set of per-giver slices for the current draw.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The tokens in effect for the keyed slices when stored; <see langword="null"/> when the token does not grant admin, or a slice is too large.</returns>
    Task<IReadOnlyList<RevealTokenForKey>?> SetRevealSlicesAsync(string adminToken, IReadOnlyList<RevealSlice> slices, CancellationToken cancellationToken = default);

    /// <summary>Publishes reveal slices for a group the caller owns (account mode, ADR-0068): the owner authorizes by group id + bearer.</summary>
    /// <param name="accountId">The owning account.</param>
    /// <param name="groupId">The group.</param>
    /// <param name="slices">The complete set of per-giver slices for the current draw.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The tokens in effect for the keyed slices when stored; <see langword="null"/> when the caller is not the owner, or a slice is too large.</returns>
    Task<IReadOnlyList<RevealTokenForKey>?> SetRevealSlicesAsOwnerAsync(Guid accountId, Guid groupId, IReadOnlyList<RevealSlice> slices, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one giver's reveal slice by its reveal token (ADR-0068) — the public reveal page's only call.
    /// The token grants exactly this one giver's opaque slice, never the draw (NFR-002).
    /// </summary>
    /// <param name="revealToken">The per-giver reveal token from a link's <c>rt=</c>.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The giver's slice, or <see langword="null"/> when the token does not resolve (unknown or revoked).</returns>
    Task<RevealSliceView?> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the reveal-link holder's own gift note and their wishlist for the token's event (ADR-0084).
    /// </summary>
    /// <param name="revealToken">The per-giver reveal token from a link's <c>rt=</c>.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The entry, or <see langword="null"/> when the token does not resolve, its slice names no member/event
    /// (an older client published it), or the member is linked to an account (they use My wishlists).
    /// </returns>
    Task<RevealEntryView?> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the reveal-link holder's own gift note and their wishlist for the token's event (ADR-0084): upserts
    /// the member's participant entry (keeping their other events' wishlists), merges it into the group body and
    /// bumps the version — as an ADR-0080 participant write. Touches nothing but that member's entry.
    /// </summary>
    /// <param name="revealToken">The per-giver reveal token from a link's <c>rt=</c>.</param>
    /// <param name="entry">The gift note and this event's wishlist.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// Applied with the new version; TooLarge over the entry limits; NotFound when the token does not resolve,
    /// names no member/event, or the member is linked to an account.
    /// </returns>
    Task<SharedWriteResult> WriteRevealEntryAsync(string revealToken, RevealEntryWrite entry, CancellationToken cancellationToken = default);
}

/// <summary>How a share-by-link create ended (ADR-0058).</summary>
public enum CreateSharedGroupStatus
{
    /// <summary>The group was stored and its links minted (or returned to the matching-key creator).</summary>
    Created,

    /// <summary>The body exceeded the NFR-009 abuse limits; nothing was stored.</summary>
    TooLarge,

    /// <summary>
    /// The create was refused: the creator key was missing, or the group already exists and the request
    /// did not present its matching creator key, or the group is account-owned. No links are disclosed.
    /// </summary>
    Refused,
}

/// <summary>The outcome of <see cref="ISharedGroupStore.CreateAsync"/>.</summary>
/// <param name="Status">How the create ended.</param>
/// <param name="Links">The minted (or resumed) links, present only when <see cref="Status"/> is <see cref="CreateSharedGroupStatus.Created"/>.</param>
public sealed record CreateSharedGroupResult(CreateSharedGroupStatus Status, MintedLinks? Links);

/// <summary>How a person-claim ended.</summary>
public enum PersonClaimStatus
{
    /// <summary>The member was claimed by this call.</summary>
    Claimed,

    /// <summary>The member was already claimed — the first-claim lock held.</summary>
    AlreadyClaimed,

    /// <summary>The token did not resolve to a group.</summary>
    NotFound,
}

/// <summary>The outcome of <see cref="ISharedGroupStore.ClaimPersonAsync"/>.</summary>
/// <param name="Status">How the claim ended.</param>
/// <param name="ParticipantKey">On a successful claim, the key that proves it later (ADR-0080).</param>
public sealed record PersonClaimResult(PersonClaimStatus Status, string? ParticipantKey = null);

/// <summary>
/// What a join-link reader or writer presented to prove which member it speaks for (ADR-0080).
/// </summary>
/// <param name="ParticipantKey">The participant key from the claim, if presented.</param>
/// <param name="AccountId">The signed-in account, if any (proves an account claim).</param>
public sealed record ParticipantProof(string? ParticipantKey, Guid? AccountId)
{
    /// <summary>Gets a proof of nothing — an unproven reader.</summary>
    public static ParticipantProof None { get; } = new(null, null);
}

/// <summary>Who organizes a stored shared group, for the linking-publish check (ADR-0080).</summary>
/// <param name="OwnerAccountId">The owning account, or <see langword="null"/> for an owner-less group.</param>
/// <param name="AdminTokenValid">Whether the presented admin token is active for this group.</param>
public sealed record SharedGroupOrganizer(Guid? OwnerAccountId, bool AdminTokenValid);

/// <summary>Why a push to a shared-by-link group ended as it did.</summary>
public enum SharedWriteStatus
{
    /// <summary>The push was stored (a clean fast-forward).</summary>
    Applied,

    /// <summary>The push arrived later by server clock and won, discarding an older concurrent body in full (ADR-0016).</summary>
    Collision,

    /// <summary>The token did not resolve to a group.</summary>
    NotFound,

    /// <summary>The token does not grant the admin capability, or has been retired by an account admin-claim.</summary>
    Forbidden,

    /// <summary>The body exceeded the NFR-009 abuse limits and was rejected; nothing was stored.</summary>
    TooLarge,
}

/// <summary>The outcome of <see cref="ISharedGroupStore.PushAsync"/>.</summary>
/// <param name="Status">How the push ended.</param>
/// <param name="Version">The version now stored (0 when nothing was stored).</param>
/// <param name="Rewritten">Whether participants' entries were re-applied to the pushed body (ADR-0080).</param>
public sealed record SharedWriteResult(SharedWriteStatus Status, long Version, bool Rewritten = false);
