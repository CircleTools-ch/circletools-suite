using System.Net.Http.Json;
using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// <see cref="ISharedGroupAccountApi"/> over the account's <strong>authorized</strong> client
/// (ADR-0058): claim-admin and release-admin are the two shared-group operations that require identity.
/// </summary>
/// <param name="client">The authorized client, carrying the account's bearer token.</param>
public sealed class HttpSharedGroupAccountApi(HttpClient client) : ISharedGroupAccountApi
{
    private const string Route = "api/v1/shared";

    /// <inheritdoc />
    public async Task<ApiResult> ClaimAdminAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Route}/{Uri.EscapeDataString(adminToken)}/claim-admin");
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
    public async Task<ApiResult<RotatedAdminLink>> ReleaseAdminAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Route}/{groupId}/release-to-link");
        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult.Failed<RotatedAdminLink>(await ApiResponseReader.ReadErrorAsync(response, cancellationToken));
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (stream.ConfigureAwait(false))
            {
                var value = await JsonSerializer.DeserializeAsync(stream, SyncJsonContext.Default.RotatedAdminLink, cancellationToken);
                return value is null
                    ? ApiResult.Failed<RotatedAdminLink>(new ApiError(ApiFailure.Unexpected, "The response was empty."))
                    : ApiResult.Ok(value);
            }
        }
        catch (HttpRequestException)
        {
            return ApiResult.Failed<RotatedAdminLink>(new ApiError(ApiFailure.Unreachable));
        }
    }

    /// <inheritdoc />
    public async Task<ApiResult<SharedGroupView>> GetOwnedAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{Route}/{groupId}/owned");
        return await SendForAsync(message, SyncJsonContext.Default.SharedGroupView, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<PushSharedGroupResult>> PushOwnedAsync(Guid groupId, PushSharedGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.PushSharedGroupRequest);
        using var message = new HttpRequestMessage(HttpMethod.Put, $"{Route}/{groupId}/owned") { Content = content };
        return await SendForAsync(message, SyncJsonContext.Default.PushSharedGroupResult, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult> DeleteOwnedAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"{Route}/{groupId}/owned");
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
    public async Task<ApiResult<OwnedSharedGroups>> ListOwnedAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{Route}/owned");
        return await SendForAsync(message, SyncJsonContext.Default.OwnedSharedGroups, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult> PublishOwnedRevealSlicesAsync(Guid groupId, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.PublishRevealSlicesRequest);
        using var message = new HttpRequestMessage(HttpMethod.Put, $"{Route}/{groupId}/owned/reveal-slices") { Content = content };
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

    private async Task<ApiResult<T>> SendForAsync<T>(
        HttpRequestMessage message,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using (message)
        {
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
                    var value = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken);
                    return value is null
                        ? ApiResult.Failed<T>(new ApiError(ApiFailure.Unexpected, "The response was empty."))
                        : ApiResult.Ok(value);
                }
            }
            catch (HttpRequestException)
            {
                return ApiResult.Failed<T>(new ApiError(ApiFailure.Unreachable));
            }
        }
    }
}
