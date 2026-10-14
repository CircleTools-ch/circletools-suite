using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Linking;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// A configurable <see cref="ILinkingApi"/> double. Only the methods a given test needs are set; the
/// rest return empty successes, so a test states exactly the surface it exercises.
/// </summary>
public sealed class FakeLinkingApi : ILinkingApi
{
    public MyAssignmentsResponse Assignments { get; set; } = new([]);
    public MyMembershipsResponse Memberships { get; set; } = new([]);
    public MemberLinkResolutionsResponse Resolutions { get; set; } = new([]);
    public List<Guid> Acked { get; } = [];
    public List<(Guid GroupId, PublishAssignmentsRequest Request)> Published { get; } = [];

    public List<SendMemberLinkRequest> SentLinks { get; } = [];

    /// <summary>The outcome the next <see cref="SendMemberLinkAsync"/> returns; defaults to "request sent".</summary>
    public MemberLinkOutcome SendOutcome { get; set; } = MemberLinkOutcome.RequestSent;

    public Task<ApiResult<SendMemberLinkResponse>> SendMemberLinkAsync(SendMemberLinkRequest request, CancellationToken cancellationToken = default)
    {
        SentLinks.Add(request);
        return Task.FromResult(ApiResult.Ok(new SendMemberLinkResponse(SendOutcome)));
    }

    public Task<ApiResult> RegisterCircleLinkAsync(RegisterCircleLinkRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok());

    public List<SelfLinkRequest> SelfLinks { get; } = [];

    public Task<ApiResult> SelfLinkAsync(SelfLinkRequest request, CancellationToken cancellationToken = default)
    {
        SelfLinks.Add(request);
        return Task.FromResult(ApiResult.Ok());
    }

    public Task<ApiResult<IReadOnlyList<PendingMemberLinkResponse>>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok<IReadOnlyList<PendingMemberLinkResponse>>([]));

    public Task<ApiResult<AcceptMemberLinkResponse>> AcceptAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok(new AcceptMemberLinkResponse(AcceptMemberLinkOutcome.Linked)));

    public Task<ApiResult> DeclineAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok());

    public Task<ApiResult> UnlinkMemberAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok());

    public List<Guid> Retracted { get; } = [];

    public Task<ApiResult> RetractGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        Retracted.Add(groupId);
        return Task.FromResult(ApiResult.Ok());
    }

    public Task<ApiResult<MemberLinkResolutionsResponse>> DrainResolutionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok(Resolutions));

    public Task<ApiResult> AckResolutionsAsync(IReadOnlyList<Guid> resolutionIds, CancellationToken cancellationToken = default)
    {
        Acked.AddRange(resolutionIds);
        return Task.FromResult(ApiResult.Ok());
    }

    public List<(Guid GroupId, PublishGroupEventsRequest Request)> PublishedEvents { get; } = [];

    public Task<ApiResult> PublishAssignmentsAsync(Guid groupId, PublishAssignmentsRequest request, CancellationToken cancellationToken = default)
    {
        Published.Add((groupId, request));
        return Task.FromResult(ApiResult.Ok());
    }

    public Task<ApiResult> PublishGroupEventsAsync(Guid groupId, PublishGroupEventsRequest request, CancellationToken cancellationToken = default)
    {
        PublishedEvents.Add((groupId, request));
        return Task.FromResult(ApiResult.Ok());
    }

    public Task<ApiResult<MyAssignmentResponse>> GetMyAssignmentAsync(Guid groupId, Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Failed<MyAssignmentResponse>(new ApiError(ApiFailure.NotFound)));

    public Task<ApiResult<MyAssignmentsResponse>> GetMyAssignmentsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok(Assignments));

    public Task<ApiResult> UpdateMyWishlistAsync(Guid groupId, Guid eventId, UpdateMyWishlistRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok());

    public Task<ApiResult<MyWishlistsResponse>> GetMyWishlistsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok(new MyWishlistsResponse([])));

    public Task<ApiResult<MyMembershipsResponse>> GetMyMembershipsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ApiResult.Ok(Memberships));
}

/// <summary>A <see cref="TimeProvider"/> pinned to a fixed instant (UTC zone) so "today" is deterministic.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>An in-memory <see cref="IGroupRepository"/> that round-trips through <see cref="GroupState"/>.</summary>
public sealed class InMemoryGroupRepository : IGroupRepository
{
    private readonly Dictionary<Guid, GroupState> _store = [];

    public Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Group>>(_store.Values.Select(Group.FromState).ToList());

    public Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(id, out var state) ? Group.FromState(state) : null);

    public Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        _store[group.Id] = group.ToState();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }
}
