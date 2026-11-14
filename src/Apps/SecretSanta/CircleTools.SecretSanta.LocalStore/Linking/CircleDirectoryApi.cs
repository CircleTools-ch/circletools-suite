// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using CircleTools.ApiClient;
using CircleTools.Circles.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Linking;

/// <summary>
/// The client's read-only port onto the caller's circles on the Main API (FEAT-005), for the
/// create-from-circle picker. It only ever <em>reads</em> — no circle data is written locally
/// (ADR-0033) — and every read is authorized per request against current membership (NFR-006).
/// </summary>
public interface ICircleDirectoryApi
{
    /// <summary>Lists the circles the caller belongs to.</summary>
    Task<ApiResult<IReadOnlyList<CircleSummaryResponse>>> GetCirclesAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads one circle and its people, so the picker can offer them as members.</summary>
    Task<ApiResult<CircleDetailResponse>> GetCircleAsync(Guid circleId, CancellationToken cancellationToken = default);
}

/// <summary>Source-generated JSON for the circle read (FEAT-005), camel-cased to match the API.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IReadOnlyList<CircleSummaryResponse>))]
[JsonSerializable(typeof(CircleDetailResponse))]
public sealed partial class CircleDirectoryJsonContext : JsonSerializerContext;

/// <summary>
/// <see cref="ICircleDirectoryApi"/> over the account's authorized <see cref="HttpClient"/>. An
/// unreachable server is <see cref="ApiFailure.Unreachable"/>, never a thrown exception.
/// </summary>
/// <param name="client">The authorized client, carrying the account's bearer token.</param>
public sealed class HttpCircleDirectoryApi(HttpClient client) : ICircleDirectoryApi
{
    private const string Circles = "api/v1/circles";

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<CircleSummaryResponse>>> GetCirclesAsync(CancellationToken cancellationToken = default) =>
        GetAsync(Circles, CircleDirectoryJsonContext.Default.IReadOnlyListCircleSummaryResponse, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<CircleDetailResponse>> GetCircleAsync(Guid circleId, CancellationToken cancellationToken = default) =>
        GetAsync($"{Circles}/{circleId}", CircleDirectoryJsonContext.Default.CircleDetailResponse, cancellationToken);

    private async Task<ApiResult<T>> GetAsync<T>(
        string route,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(route, cancellationToken);
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
        catch (HttpRequestException)
        {
            return ApiResult.Failed<T>(new ApiError(ApiFailure.Unreachable));
        }
    }
}
