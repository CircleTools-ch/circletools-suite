using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AppSuite.ApiClient;
using AppSuite.SecretSanta.Contracts;

namespace AppSuite.SecretSanta.LocalStore.Linking;

/// <summary>
/// <see cref="ILinkingApi"/> over an authorized <see cref="HttpClient"/> (FEAT-005). Shaped like the
/// other typed clients: an unreachable server is <see cref="ApiFailure.Unreachable"/>, not a thrown
/// exception. Bodies serialize through the source-generated <see cref="LinkingJsonContext"/> so the
/// trimmed WebAssembly build keeps them.
/// </summary>
/// <param name="client">The authorized client, carrying the account's bearer token.</param>
public sealed class HttpLinkingApi(HttpClient client) : ILinkingApi
{
    private const string MemberLinks = "api/v1/member-links";
    private const string Groups = "api/v1/groups";

    /// <inheritdoc />
    public Task<ApiResult<SendMemberLinkResponse>> SendMemberLinkAsync(SendMemberLinkRequest request, CancellationToken cancellationToken = default) =>
        PostAsync(MemberLinks, request, LinkingJsonContext.Default.SendMemberLinkRequest, LinkingJsonContext.Default.SendMemberLinkResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> RegisterCircleLinkAsync(RegisterCircleLinkRequest request, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            HttpMethod.Post,
            $"{MemberLinks}/circle",
            JsonContent.Create(request, LinkingJsonContext.Default.RegisterCircleLinkRequest),
            cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<PendingMemberLinkResponse>>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        GetAsync($"{MemberLinks}/pending", LinkingJsonContext.Default.IReadOnlyListPendingMemberLinkResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<AcceptMemberLinkResponse>> AcceptAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        PostNoBodyAsync($"{MemberLinks}/{requestId}/accept", LinkingJsonContext.Default.AcceptMemberLinkResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> DeclineAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"{MemberLinks}/{requestId}/decline", content: null, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> UnlinkMemberAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"{Groups}/{groupId}/members/{memberId}/link", content: null, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MemberLinkResolutionsResponse>> DrainResolutionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync($"{MemberLinks}/resolutions", LinkingJsonContext.Default.MemberLinkResolutionsResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> AckResolutionsAsync(IReadOnlyList<Guid> resolutionIds, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            HttpMethod.Post,
            $"{MemberLinks}/resolutions/ack",
            JsonContent.Create(new AckMemberLinkResolutionsRequest(resolutionIds), LinkingJsonContext.Default.AckMemberLinkResolutionsRequest),
            cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> PublishAssignmentsAsync(Guid groupId, PublishAssignmentsRequest request, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            HttpMethod.Post,
            $"{Groups}/{groupId}/assignments/publish",
            JsonContent.Create(request, LinkingJsonContext.Default.PublishAssignmentsRequest),
            cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MyAssignmentResponse>> GetMyAssignmentAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        GetAsync($"{Groups}/{groupId}/my-assignment", LinkingJsonContext.Default.MyAssignmentResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MyAssignmentsResponse>> GetMyAssignmentsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("api/v1/my-assignments", LinkingJsonContext.Default.MyAssignmentsResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> UpdateMyWishlistAsync(Guid groupId, UpdateMyWishlistRequest request, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            HttpMethod.Put,
            $"{Groups}/{groupId}/my-wishlist",
            JsonContent.Create(request, LinkingJsonContext.Default.UpdateMyWishlistRequest),
            cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MyWishlistsResponse>> GetMyWishlistsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("api/v1/my-wishlists", LinkingJsonContext.Default.MyWishlistsResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MyMembershipsResponse>> GetMyMembershipsAsync(CancellationToken cancellationToken = default) =>
        GetAsync("api/v1/my-memberships", LinkingJsonContext.Default.MyMembershipsResponse, cancellationToken);

    private async Task<ApiResult<T>> GetAsync<T>(string route, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(route, cancellationToken);
            return await ReadAsync(response, typeInfo, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<T>(new ApiError(ApiFailure.Unreachable));
        }
    }

    private async Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string route,
        TRequest request,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(request, requestType),
        };

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            return await ReadAsync(response, responseType, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<TResponse>(new ApiError(ApiFailure.Unreachable));
        }
    }

    private async Task<ApiResult<TResponse>> PostNoBodyAsync<TResponse>(
        string route,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsync(route, content: null, cancellationToken);
            return await ReadAsync(response, responseType, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<TResponse>(new ApiError(ApiFailure.Unreachable));
        }
    }

    private async Task<ApiResult> SendNoContentAsync(HttpMethod method, string route, HttpContent? content, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, route) { Content = content };

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            return await ApiResponseReader.ReadAsync(response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed(new ApiError(ApiFailure.Unreachable));
        }
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult.Failed<T>(await ApiResponseReader.ReadErrorAsync(response, cancellationToken));
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (stream.ConfigureAwait(false))
        {
            var value = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
            return value is null
                ? ApiResult.Failed<T>(new ApiError(ApiFailure.Unexpected, "The response body was empty."))
                : ApiResult.Ok(value);
        }
    }
}
