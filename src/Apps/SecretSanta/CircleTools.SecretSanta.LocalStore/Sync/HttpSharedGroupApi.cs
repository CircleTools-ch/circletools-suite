using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// <see cref="ISharedGroupApi"/> over an <strong>anonymous</strong> <see cref="HttpClient"/> (ADR-0058,
/// FEAT-013): the share token is the credential, so this client must never attach a bearer — the host
/// wires it over <c>AccountApiClients.Anonymous</c>. An unreachable server is
/// <see cref="ApiFailure.Unreachable"/>; a 404 is <see cref="ApiFailure.NotFound"/> (the group is
/// gone), which the engine reads as "fall back to on-this-device".
/// </summary>
/// <param name="client">The anonymous client (no bearer).</param>
public sealed class HttpSharedGroupApi(HttpClient client) : ISharedGroupApi
{
    /// <summary>The shared-by-link route (ADR-0010 versioning).</summary>
    public const string Route = "api/v1/shared";

    /// <inheritdoc />
    public async Task<ApiResult<MintedLinks>> CreateAsync(
        CreateSharedGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.CreateSharedGroupRequest);
        return await SendForAsync(new HttpRequestMessage(HttpMethod.Post, Route) { Content = content },
            SyncJsonContext.Default.MintedLinks, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<SharedGroupView>> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        return await SendForAsync(new HttpRequestMessage(HttpMethod.Get, $"{Route}/{Uri.EscapeDataString(token)}"),
            SyncJsonContext.Default.SharedGroupView, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<PushSharedGroupResult>> PushAsync(
        string token,
        PushSharedGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.PushSharedGroupRequest);
        return await SendForAsync(
            new HttpRequestMessage(HttpMethod.Put, $"{Route}/{Uri.EscapeDataString(token)}") { Content = content },
            SyncJsonContext.Default.PushSharedGroupResult, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<RotatedAdminLink>> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        return await SendForAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{Route}/{Uri.EscapeDataString(adminToken)}/rotate-admin"),
            SyncJsonContext.Default.RotatedAdminLink, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult> DeleteAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"{Route}/{Uri.EscapeDataString(adminToken)}");
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
    public async Task<ApiResult<ClaimedMembers>> GetClaimsAsync(string token, CancellationToken cancellationToken = default)
    {
        return await SendForAsync(new HttpRequestMessage(HttpMethod.Get, $"{Route}/{Uri.EscapeDataString(token)}/claims"),
            SyncJsonContext.Default.ClaimedMembers, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult<PersonClaimResponse>> ClaimPersonAsync(
        string token,
        ClaimPersonRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.ClaimPersonRequest);
        return await SendForAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{Route}/{Uri.EscapeDataString(token)}/claims") { Content = content },
            SyncJsonContext.Default.PersonClaimResponse, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ApiResult> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"{Route}/{Uri.EscapeDataString(adminToken)}/claims/{memberId}");
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
    public async Task<ApiResult> PublishRevealSlicesAsync(
        string adminToken,
        PublishRevealSlicesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = JsonContent.Create(request, SyncJsonContext.Default.PublishRevealSlicesRequest);
        using var message = new HttpRequestMessage(
            HttpMethod.Put, $"{Route}/{Uri.EscapeDataString(adminToken)}/reveal-slices") { Content = content };
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
    public async Task<ApiResult<RevealSliceView>> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        return await SendForAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{Route}/reveal/{Uri.EscapeDataString(revealToken)}"),
            SyncJsonContext.Default.RevealSliceView, cancellationToken);
    }

    private async Task<ApiResult<T>> SendForAsync<T>(
        HttpRequestMessage message,
        JsonTypeInfo<T> typeInfo,
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
