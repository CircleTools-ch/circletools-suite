// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;

namespace CircleTools.ApiClient;

/// <summary>
/// Turns an <see cref="HttpResponseMessage"/> into an <see cref="ApiResult"/>, including the
/// <c>ProblemDetails</c> the API returns for failures (ADR-0010).
/// </summary>
public static class ApiResponseReader
{
    /// <summary>The serializer settings the API speaks: camelCase, case-insensitive on the way in.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>Reads a response that carries no body.</summary>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A success, or the parsed failure.</returns>
    public static async Task<ApiResult> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.IsSuccessStatusCode
            ? ApiResult.Ok()
            : ApiResult.Failed(await ReadErrorAsync(response, cancellationToken));
    }

    /// <summary>Reads a response that carries a JSON body.</summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The deserialized value, or the parsed failure.</returns>
    public static async Task<ApiResult<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            return ApiResult.Failed<T>(await ReadErrorAsync(response, cancellationToken));
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (stream.ConfigureAwait(false))
        {
            var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
            return value is null
                ? ApiResult.Failed<T>(new ApiError(ApiFailure.Unexpected, "The response body was empty."))
                : ApiResult.Ok(value);
        }
    }

    /// <summary>Reads a response body that is expected to be raw JSON, passed through untouched.</summary>
    /// <remarks>
    /// The WebAuthn options endpoints hand back the browser's own <c>PublicKeyCredential*Options</c>
    /// as opaque JSON. Deserializing it into a DTO would only risk losing a field the browser needs.
    /// </remarks>
    /// <param name="response">The response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The raw JSON, or the parsed failure.</returns>
    public static async Task<ApiResult<string>> ReadRawJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!response.IsSuccessStatusCode)
        {
            return ApiResult.Failed<string>(await ReadErrorAsync(response, cancellationToken));
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ApiResult.Ok(json);
    }

    /// <summary>Maps a failed response to an <see cref="ApiError"/>.</summary>
    /// <param name="response">The failed response.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parsed failure.</returns>
    public static async Task<ApiError> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var failure = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ApiFailure.Validation,
            HttpStatusCode.Unauthorized => ApiFailure.Unauthorized,
            HttpStatusCode.Forbidden => ApiFailure.Forbidden,
            HttpStatusCode.NotFound => ApiFailure.NotFound,
            HttpStatusCode.Conflict => ApiFailure.Conflict,
            HttpStatusCode.TooManyRequests => ApiFailure.RateLimited,
            _ => ApiFailure.Unexpected,
        };

        var retryAfter = response.Headers.RetryAfter?.Delta;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return Parse(failure, body, retryAfter);
    }

    /// <summary>
    /// Pulls the message, the refusal code and any per-field errors out of a failure body.
    /// </summary>
    /// <remarks>
    /// Three shapes turn up: <c>ValidationProblem</c> (an <c>errors</c> map), plain
    /// <c>ProblemDetails</c> (<c>title</c> / <c>detail</c>, plus a <c>code</c> extension), and the
    /// rate limiter's bare <c>{ "message": …, "code": … }</c>. Anything else — an HTML error page
    /// from a proxy, an empty body — is reported as the status code alone rather than guessed at.
    /// <para>
    /// <c>code</c> is read from both shapes because it is the only part of a refusal a multilingual
    /// client can act on: the status alone cannot tell "this circle is full" from "you already own
    /// as many circles as you can", and the title is English (ADR-0011).
    /// </para>
    /// </remarks>
    private static ApiError Parse(ApiFailure failure, string body, TimeSpan? retryAfter)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ApiError(failure, RetryAfter: retryAfter);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ApiError(failure, RetryAfter: retryAfter);
            }

            var root = document.RootElement;
            var detail = ReadString(root, "detail") ?? ReadString(root, "title") ?? ReadString(root, "message");
            var errors = ReadErrors(root);

            return new ApiError(failure, detail, errors, retryAfter, ReadString(root, "code"));
        }
        catch (JsonException)
        {
            return new ApiError(failure, RetryAfter: retryAfter);
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static Dictionary<string, IReadOnlyList<string>>? ReadErrors(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var messages = field.Value
                .EnumerateArray()
                .Where(m => m.ValueKind == JsonValueKind.String)
                .Select(m => m.GetString()!)
                .ToArray();

            if (messages.Length > 0)
            {
                result[field.Name] = messages;
            }
        }

        return result.Count > 0 ? result : null;
    }
}
