using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>
/// <see cref="IOnlineGroupApi"/> over the authorized <see cref="HttpClient"/> (ADR-0086): the bearer goes along
/// whenever someone is signed in, and the organizer token or device key travel in headers, never in the URL.
/// Bodies go through the source-generated <see cref="OnlineJsonContext"/> (trimmed WebAssembly, iOS AOT — ADR-0072).
/// </summary>
/// <param name="client">The authorized client.</param>
public sealed class HttpOnlineGroupApi(HttpClient client) : IOnlineGroupApi
{
    /// <summary>The route prefix.</summary>
    public const string Route = "api/v1/secretsanta";

    /// <summary>The header the organizer link's token travels in.</summary>
    public const string OrganizerTokenHeader = "X-Organizer-Token";

    /// <summary>The header a device key travels in.</summary>
    public const string DeviceKeyHeader = "X-Device-Key";

    private static OnlineJsonContext Json => OnlineJsonContext.Default;

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> CreateAsync(CreateOnlineGroupRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "groups", Body(request, Json.CreateOnlineGroupRequest), OnlineCredentials.None, Json.GroupLinks, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OrganizerGroupView>> GetAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"groups/{groupId}", null, credentials, Json.OrganizerGroupView, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<PushOnlineGroupResponse>> PushAsync(Guid groupId, PushOnlineGroupRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"groups/{groupId}", Body(request, Json.PushOnlineGroupRequest), credentials, Json.PushOnlineGroupResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"groups/{groupId}", null, credentials, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> SetJoinLinkAsync(Guid groupId, JoinLinkRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"groups/{groupId}/join-link", Body(request, Json.JoinLinkRequest), credentials, Json.GroupLinks, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> RotateOrganizerLinkAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"groups/{groupId}/organizer-link/rotate", null, credentials, Json.GroupLinks, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> TakeOverAsync(Guid groupId, string organizerToken, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"groups/{groupId}/take-over", null, new OnlineCredentials(organizerToken), cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> HandOverAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"groups/{groupId}/hand-over", null, OnlineCredentials.None, Json.GroupLinks, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> AddParticipantAsync(Guid groupId, AddParticipantRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"groups/{groupId}/participants", Body(request, Json.AddParticipantRequest), credentials, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> ReleaseAsync(Guid groupId, Guid memberId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"groups/{groupId}/participants/{memberId}", null, credentials, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<RevealTokenView>>> GetRevealTokensAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"groups/{groupId}/reveal-tokens", null, credentials, Json.IReadOnlyListRevealTokenView, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ParticipantView>> GetParticipantViewAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"groups/{groupId}/me", null, credentials, Json.ParticipantView, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<OwnEntryResponse>> WriteOwnEntryAsync(Guid groupId, OwnEntryRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"groups/{groupId}/me/entry", Body(request, Json.OwnEntryRequest), credentials, Json.OwnEntryResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> LeaveAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"groups/{groupId}/me", null, credentials, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<JoinPreview>> PreviewJoinAsync(string joinToken, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"join/{Uri.EscapeDataString(joinToken)}", null, OnlineCredentials.None, Json.JoinPreview, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<JoinResponse>> JoinAsync(string joinToken, JoinRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"join/{Uri.EscapeDataString(joinToken)}", Body(request, Json.JoinRequest), OnlineCredentials.None, Json.JoinResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<MyGroups>> GetMyGroupsAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, "me/groups", null, OnlineCredentials.None, Json.MyGroups, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> AnswerInvitationAsync(Guid groupId, bool accept, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"me/invitations/{groupId}/{(accept ? "accept" : "decline")}", null, OnlineCredentials.None, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<RevealPayload>> GetRevealAsync(string revealToken, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"reveal/{Uri.EscapeDataString(revealToken)}", null, OnlineCredentials.None, Json.RevealPayload, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<RevealEntry>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Get, $"reveal/{Uri.EscapeDataString(revealToken)}/entry", null, OnlineCredentials.None, Json.RevealEntry, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult> WriteRevealEntryAsync(string revealToken, RevealEntry entry, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"reveal/{Uri.EscapeDataString(revealToken)}/entry", Body(entry, Json.RevealEntry), OnlineCredentials.None, cancellationToken);

    private static JsonContent Body<T>(T value, JsonTypeInfo<T> type) => JsonContent.Create(value, type);

    private static HttpRequestMessage Message(HttpMethod method, string path, HttpContent? content, OnlineCredentials credentials)
    {
        var message = new HttpRequestMessage(method, $"{Route}/{path}") { Content = content };
        if (credentials.OrganizerToken is { } organizer)
        {
            message.Headers.Add(OrganizerTokenHeader, organizer);
        }

        if (credentials.DeviceKey is { } key)
        {
            message.Headers.Add(DeviceKeyHeader, key);
        }

        return message;
    }

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, HttpContent? content, OnlineCredentials credentials, CancellationToken cancellationToken)
    {
        using var message = Message(method, path, content, credentials);
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            return response.IsSuccessStatusCode
                ? ApiResult.Ok()
                : ApiResult.Failed(await ApiResponseReader.ReadErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed(new ApiError(ApiFailure.Unreachable));
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string path, HttpContent? content, OnlineCredentials credentials, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        using var message = Message(method, path, content, credentials);
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Failed<T>(await ApiResponseReader.ReadErrorAsync(response, cancellationToken));
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (stream.ConfigureAwait(false))
            {
                var value = await JsonSerializer.DeserializeAsync(stream, type, cancellationToken);
                return value is null
                    ? ApiResult.Failed<T>(new ApiError(ApiFailure.Unexpected, "The response body was empty."))
                    : ApiResult.Ok(value);
            }
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<T>(new ApiError(ApiFailure.Unreachable));
        }
    }
}
