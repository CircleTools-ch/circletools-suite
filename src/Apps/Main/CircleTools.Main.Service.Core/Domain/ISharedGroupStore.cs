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
    /// <param name="request">The group id, version and opaque body.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The minted links, or <see langword="null"/> when the body exceeds the NFR-009 abuse limits.
    /// Re-creating a group already shared returns its current active links (idempotent share).
    /// </returns>
    Task<MintedLinks?> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Resolves a token to the group it addresses and the capability it grants.</summary>
    /// <param name="token">The admin or join token from a link.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The group view, or <see langword="null"/> when the token does not resolve — a wrong, disabled,
    /// or never-existed token are indistinguishable (no oracle). A retired admin token resolves to a
    /// view with <see cref="SharedGroupView.AdminRetired"/> set, so the client can explain it.
    /// </returns>
    Task<SharedGroupView?> GetAsync(string token, CancellationToken cancellationToken = default);

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

    /// <summary>Claims a member for the caller through the join link — first claim wins and locks (ADR-0058).</summary>
    /// <param name="token">An active join or admin token for the group.</param>
    /// <param name="request">Which member, and how the claim is remembered (device or account).</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The claim outcome (claimed / already-claimed / not-found).</returns>
    Task<PersonClaimResult> ClaimPersonAsync(string token, ClaimPersonRequest request, CancellationToken cancellationToken = default);

    /// <summary>Releases a claimed member so it can be claimed again — admin only, the sole recovery path (ADR-0058).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the member was released (or was already free); <see langword="false"/> when the token does not grant admin.</returns>
    Task<bool> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default);

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
}

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
public sealed record PersonClaimResult(PersonClaimStatus Status);

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
public sealed record SharedWriteResult(SharedWriteStatus Status, long Version);
