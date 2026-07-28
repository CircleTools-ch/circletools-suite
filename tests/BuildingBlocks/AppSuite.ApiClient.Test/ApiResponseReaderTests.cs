using System.Net;

namespace AppSuite.ApiClient.Test;

/// <summary>
/// Tests for reading a failed response. The API answers with <c>ProblemDetails</c> (ADR-0010), the
/// rate limiter with a bare message, and a proxy in between may answer with neither — all three end
/// up as an <see cref="ApiError"/> the UI can act on.
/// </summary>
public sealed class ApiResponseReaderTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ApiFailure.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, ApiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ApiFailure.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ApiFailure.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ApiFailure.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests, ApiFailure.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, ApiFailure.Unexpected)]
    public async Task StatusCode_MapsToAFailureTheUiCanSwitchOn(HttpStatusCode status, ApiFailure expected)
    {
        using var response = new HttpResponseMessage(status) { Content = new StringContent(string.Empty) };

        var error = await ApiResponseReader.ReadErrorAsync(response);

        Assert.Equal(expected, error.Failure);
    }

    [Fact]
    public async Task ValidationProblem_KeepsThePerFieldMessages()
    {
        using var response = Json(
            HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","errors":{"password":["Too short.","No digit."]}}""");

        var error = await ApiResponseReader.ReadErrorAsync(response);

        Assert.Equal(ApiFailure.Validation, error.Failure);
        Assert.Equal(["Too short.", "No digit."], error.Errors["password"]);
    }

    [Fact]
    public async Task RateLimiterMessage_IsRecognisedEvenThoughItIsNotProblemDetails()
    {
        using var response = Json(HttpStatusCode.TooManyRequests, """{"message":"Too many requests."}""");
        response.Headers.Add("Retry-After", "60");

        var error = await ApiResponseReader.ReadErrorAsync(response);

        Assert.Equal(ApiFailure.RateLimited, error.Failure);
        Assert.Equal("Too many requests.", error.Detail);
        Assert.Equal(TimeSpan.FromSeconds(60), error.RetryAfter);
    }

    [Fact]
    public async Task NonJsonBody_IsReportedAsTheStatusAloneRatherThanGuessedAt()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>"),
        };

        var error = await ApiResponseReader.ReadErrorAsync(response);

        Assert.Equal(ApiFailure.Unexpected, error.Failure);
        Assert.Null(error.Detail);
        Assert.Empty(error.Errors);
    }

    [Fact]
    public async Task SuccessWithABody_IsDeserialized()
    {
        using var response = Json(HttpStatusCode.OK, """{"name":"Anna","count":2}""");

        var result = await ApiResponseReader.ReadAsync<Sample>(response);

        Assert.True(result.IsSuccess);
        Assert.Equal("Anna", result.Value!.Name);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task RawJson_IsPassedThroughUntouched()
    {
        const string body = """{"challenge":"abc","rp":{"id":"localhost"}}""";
        using var response = Json(HttpStatusCode.OK, body);

        var result = await ApiResponseReader.ReadRawJsonAsync(response);

        // The WebAuthn options are the browser's own wire format; round-tripping them through a DTO
        // could only lose a field the browser needs.
        Assert.Equal(body, result.Value);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed record Sample(string Name, int Count);
}
