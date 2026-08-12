using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AppSuite.Ui.Abstractions;
using Microsoft.Extensions.Localization;

namespace AppSuite.Main.Ui.Test.TestSupport;

/// <summary>
/// Echoes the resource key back as its value. Component tests assert on structure and behaviour;
/// key parity across DE/EN/FR is checked separately by <c>LocalizationParityTests</c>.
/// </summary>
/// <typeparam name="T">The localizer marker type.</typeparam>
public sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
{
    /// <inheritdoc />
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), resourceNotFound: false);

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

/// <summary>A stand-in API: answers each route from a table, and records what it was asked.</summary>
public sealed class StubApiHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The routes that were called, as <c>METHOD /path</c>, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// When set, every call throws as an unreachable server does — the client turns it into
    /// <c>ApiFailure.Unreachable</c>, which is how a page tells "offline" from a real failure.
    /// </summary>
    public bool Offline { get; set; }

    /// <summary>Answers a route with a status and no body.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path, without a leading slash.</param>
    /// <param name="status">The status to answer with.</param>
    /// <returns>The same handler, for chaining.</returns>
    public StubApiHandler Respond(HttpMethod method, string path, HttpStatusCode status)
    {
        _routes[Key(method, path)] = () => new HttpResponseMessage(status);
        return this;
    }

    /// <summary>Answers a route with a serialized body.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path, without a leading slash.</param>
    /// <param name="body">The value to serialize.</param>
    /// <param name="status">The status to answer with.</param>
    /// <returns>The same handler, for chaining.</returns>
    public StubApiHandler RespondJson<T>(
        HttpMethod method,
        string path,
        T body,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes[Key(method, path)] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json"),
        };

        return this;
    }

    /// <summary>
    /// Answers a route with a <c>ProblemDetails</c> carrying a stable refusal code, the way the
    /// circle endpoints refuse (FEAT-003, ADR-0011).
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path, without a leading slash.</param>
    /// <param name="status">The status to answer with.</param>
    /// <param name="code">The refusal code the UI translates.</param>
    /// <returns>The same handler, for chaining.</returns>
    public StubApiHandler RespondProblem(
        HttpMethod method,
        string path,
        HttpStatusCode status,
        string code)
    {
        _routes[Key(method, path)] = () => new HttpResponseMessage(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { title = "Refused.", code }, _json),
                Encoding.UTF8,
                "application/problem+json"),
        };

        return this;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("The stub is offline.");
        }

        var key = Key(request.Method, request.RequestUri!.AbsolutePath.TrimStart('/'));
        Calls.Add(key);

        // An unscripted route is a test that reached somewhere it did not mean to; 404 makes that
        // visible as a failing assertion rather than a hang or a null.
        return Task.FromResult(_routes.TryGetValue(key, out var factory)
            ? factory()
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static string Key(HttpMethod method, string path) => $"{method.Method} {path.TrimStart('/')}";
}

/// <summary>A passkey ceremony with a scripted answer, so no authenticator is involved.</summary>
/// <param name="result">The credential JSON, or <see langword="null"/> to act as a cancellation.</param>
/// <param name="throws">Whether the ceremony should fail outright instead.</param>
public sealed class StubPasskeyCeremony(string? result = null, bool throws = false) : IPasskeyCeremony
{
    /// <inheritdoc />
    public ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        Answer();

    /// <inheritdoc />
    public ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        Answer();

    private ValueTask<string?> Answer() => throws
        ? throw new PasskeyCeremonyException("No authenticator here.")
        : ValueTask.FromResult(result);
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> with no browser behind it.</summary>
public sealed class FakeKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<(StorageScope Scope, string Key), string> _values = [];

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_values.TryGetValue((scope, key), out var value) ? value : null);

    /// <inheritdoc />
    public ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        _values[(scope, key)] = value;
        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
    {
        _values.Remove((scope, key));
        return ValueTask.CompletedTask;
    }
}

/// <summary>Records what the UI asked to be saved, instead of downloading it.</summary>
public sealed class FakeFileSaver : IFileSaver
{
    /// <summary>The last file the UI handed over, if any.</summary>
    public (string FileName, string ContentType, string Content)? Saved { get; private set; }

    /// <inheritdoc />
    public ValueTask SaveAsync(
        string fileName,
        string contentType,
        string content,
        CancellationToken cancellationToken = default)
    {
        Saved = (fileName, contentType, content);
        return ValueTask.CompletedTask;
    }
}
