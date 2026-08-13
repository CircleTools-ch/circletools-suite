using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// <see cref="ISyncApi"/> over an authorized <see cref="HttpClient"/> (FEAT-004). Shaped like the
/// other typed clients: an unreachable server is <see cref="ApiFailure.Unreachable"/>, not a thrown
/// exception, so a cycle that cannot reach the server ends cleanly with nothing cleared.
/// </summary>
/// <remarks>
/// The bearer token and its transparent refresh are the supplied client's concern (ADR-0021): the
/// host wires this over the account's authorized <see cref="HttpClient"/>. Bodies serialize through
/// the source-generated context so the trimmed WebAssembly build keeps them.
/// </remarks>
/// <param name="client">The authorized client, carrying the account's bearer token.</param>
public sealed class HttpSyncApi(HttpClient client) : ISyncApi
{
    /// <summary>The delta-exchange route (ADR-0010 versioning).</summary>
    public const string Route = "api/v1/sync";

    /// <summary>The per-group route prefix; the group id (and, for a claim, <c>/claim</c>) is appended (FR-025/FR-026).</summary>
    public const string GroupsRoute = $"{Route}/groups";

    /// <inheritdoc />
    public async Task<ApiResult<SyncResponse>> ExchangeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = JsonContent.Create(request, SyncJsonContext.Default.SyncRequest);
        using var message = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Failed<SyncResponse>(
                    await ApiResponseReader.ReadErrorAsync(response, cancellationToken));
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (stream.ConfigureAwait(false))
            {
                var value = await JsonSerializer.DeserializeAsync(
                    stream, SyncJsonContext.Default.SyncResponse, cancellationToken);
                return value is null
                    ? ApiResult.Failed<SyncResponse>(new ApiError(ApiFailure.Unexpected, "The sync response was empty."))
                    : ApiResult.Ok(value);
            }
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<SyncResponse>(new ApiError(ApiFailure.Unreachable));
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult> ClaimAsync(
        Guid groupId,
        ClaimGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = JsonContent.Create(request, SyncJsonContext.Default.ClaimGroupRequest);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{GroupsRoute}/{groupId}/claim")
        {
            Content = content,
        };

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

    /// <inheritdoc />
    public async Task<ApiResult> UnclaimAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{GroupsRoute}/{groupId}");

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
}
