using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Linking;

/// <summary>
/// The client's port onto the M4 linking surface on the Main API (FEAT-005) — member-links, the
/// assignment projection and self-owned wishlists. A port rather than a bare <c>HttpClient</c> so the
/// client-side orchestration (resolution application, assignment publication) can be driven in a unit
/// test with no transport, and so an unreachable server is an <see cref="ApiResult"/> value rather than
/// an exception that aborts a sync cycle.
/// </summary>
public interface ILinkingApi
{
    /// <summary>Sends a member-link request (organizer → an email). Reveals nothing about account existence (NFR-005).</summary>
    Task<ApiResult<SendMemberLinkResponse>> SendMemberLinkAsync(SendMemberLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Registers a create-from-circle link (both accounts share the circle; the server verifies it).</summary>
    Task<ApiResult> RegisterCircleLinkAsync(RegisterCircleLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lists the member-link requests waiting for the signed-in caller.</summary>
    Task<ApiResult<IReadOnlyList<PendingMemberLinkResponse>>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Accepts a member-link request the caller received.</summary>
    Task<ApiResult<AcceptMemberLinkResponse>> AcceptAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Declines a member-link request the caller received. Indistinguishable from expiry to the organizer.</summary>
    Task<ApiResult> DeclineAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Unlinks a member the caller organizes, by group and member (the pair the organizer's client holds).</summary>
    Task<ApiResult> UnlinkMemberAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>Drains the pending link resolutions for the caller's own groups, to apply on the sync cycle.</summary>
    Task<ApiResult<MemberLinkResolutionsResponse>> DrainResolutionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Acknowledges resolutions once applied to the group document.</summary>
    Task<ApiResult> AckResolutionsAsync(IReadOnlyList<Guid> resolutionIds, CancellationToken cancellationToken = default);

    /// <summary>Publishes the assignment projection for a group after a draw or re-run.</summary>
    Task<ApiResult> PublishAssignmentsAsync(Guid groupId, PublishAssignmentsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the caller's own assignment in one group. A 404 (no draw yet, or unlinked) surfaces as
    /// <see cref="ApiFailure.NotFound"/>; a 403 (stranger to the group) as <see cref="ApiFailure.Forbidden"/>.
    /// </summary>
    Task<ApiResult<MyAssignmentResponse>> GetMyAssignmentAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Reads the caller's assignments across every group they are linked in (one per group).</summary>
    Task<ApiResult<MyAssignmentsResponse>> GetMyAssignmentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores the caller's own wishlist for one membership.</summary>
    Task<ApiResult> UpdateMyWishlistAsync(Guid groupId, UpdateMyWishlistRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the caller's self-owned wishlists across their memberships (for the copy-previous offer).</summary>
    Task<ApiResult<MyWishlistsResponse>> GetMyWishlistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the groups the caller is a linked member of, so a wishlist can be added even before the draw.</summary>
    Task<ApiResult<MyMembershipsResponse>> GetMyMembershipsAsync(CancellationToken cancellationToken = default);
}
