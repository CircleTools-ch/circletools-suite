using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The client's port onto the account-free shared-by-link endpoints (ADR-0058, FEAT-013,
/// <c>/api/v1/shared</c>). Unlike <see cref="ISyncApi"/> these calls are token-addressed and carry no
/// bearer — the share token is the credential. An unreachable server is an <see cref="ApiResult"/>
/// value the engine inspects, never an exception that leaves local state half-flipped.
/// </summary>
public interface ISharedGroupApi
{
    /// <summary>Uploads a group into shared-by-link storage and mints its admin + join links.</summary>
    Task<ApiResult<MintedLinks>> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fetches the group a token addresses (or a not-found the caller reads as "gone").</summary>
    Task<ApiResult<SharedGroupView>> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a group by its join token as the member <paramref name="participantKey"/> proves (ADR-0080): the
    /// server then includes that member's own pairing and entry.
    /// </summary>
    /// <param name="token">The join token.</param>
    /// <param name="participantKey">The participant key from the claim.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The trimmed group view.</returns>
    Task<ApiResult<SharedGroupView>> GetAsParticipantAsync(string token, string participantKey, CancellationToken cancellationToken = default);

    /// <summary>Writes the participant's own entry — gift note and per-event wishlists (ADR-0080).</summary>
    /// <param name="token">The join token.</param>
    /// <param name="participantKey">The participant key from the claim.</param>
    /// <param name="entry">The whole entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The group version now stored.</returns>
    Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string token, string participantKey, ParticipantEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Leaves the group: releases the participant's own claim and entry (ADR-0080, Q1).</summary>
    /// <param name="token">The join token.</param>
    /// <param name="participantKey">The participant key from the claim.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success when the claim was released.</returns>
    Task<ApiResult> LeaveAsync(string token, string participantKey, CancellationToken cancellationToken = default);

    /// <summary>Exchanges a pre-ADR-0080 device claim's handle for a participant key, once.</summary>
    /// <param name="token">The join token.</param>
    /// <param name="memberId">The claimed member.</param>
    /// <param name="deviceHandle">The handle the device claimed with.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The key, or a failure when refused.</returns>
    Task<ApiResult<ParticipantKeyResponse>> IssueParticipantKeyAsync(string token, Guid memberId, string deviceHandle, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for the group an admin token addresses.</summary>
    Task<ApiResult<PushSharedGroupResult>> PushAsync(string token, PushSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Disables the current admin link and mints a fresh one.</summary>
    Task<ApiResult<RotatedAdminLink>> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Deletes the online copy of a group and all its links (owner-less delete, NFR-010).</summary>
    Task<ApiResult> DeleteAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Reads which members are already claimed, so the join UI can lock them.</summary>
    Task<ApiResult<ClaimedMembers>> GetClaimsAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Claims a member ("this is me") through the join link — first claim wins.</summary>
    Task<ApiResult<PersonClaimResponse>> ClaimPersonAsync(string token, ClaimPersonRequest request, CancellationToken cancellationToken = default);

    /// <summary>Releases a claimed member (admin only), so it can be claimed again.</summary>
    Task<ApiResult> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the full current set of per-giver reveal slices for the group an admin token addresses
    /// (ADR-0068, FEAT-020), so its backend-backed reveal links show the current wishlist/event. The set
    /// is authoritative — stale reveal tokens from a previous draw stop resolving.
    /// </summary>
    /// <returns>The tokens in effect for keyed slices — an existing token wins over the one sent (ADR-0068 amendment).</returns>
    Task<ApiResult<PublishRevealSlicesResponse>> PublishRevealSlicesAsync(string adminToken, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one giver's live reveal slice by its reveal token (ADR-0068) — what the reveal page fetches when online.</summary>
    Task<ApiResult<RevealSliceView>> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the reveal-link holder's own gift note and their wishlist for the token's event (ADR-0084) — what
    /// the reveal page offers for editing. Not found when the link has no editor (older slice, linked member).
    /// </summary>
    /// <param name="revealToken">The link's reveal token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The holder's entry.</returns>
    Task<ApiResult<RevealEntryView>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>Replaces the reveal-link holder's own gift note and their wishlist for the token's event (ADR-0084).</summary>
    /// <param name="revealToken">The link's reveal token.</param>
    /// <param name="entry">The gift note and this event's wishlist.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The group version now stored.</returns>
    Task<ApiResult<ParticipantEntryResult>> WriteRevealEntryAsync(string revealToken, RevealEntryWrite entry, CancellationToken cancellationToken = default);
}
