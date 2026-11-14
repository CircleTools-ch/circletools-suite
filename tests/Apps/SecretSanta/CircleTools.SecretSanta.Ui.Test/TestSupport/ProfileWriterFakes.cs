// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Ui.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// A stand-in for the Main account API's <c>/profile</c> routes (FEAT-004): answers <c>GET</c> from a
/// stored profile and applies a <c>PUT</c> to it, and can be switched offline to make a transport
/// failure, which the client turns into <c>Unreachable</c>.
/// </summary>
public sealed class AccountApiStub : HttpMessageHandler
{
    private const string ProfilePath = "api/v1/account/profile";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>When set, every call throws as an unreachable server does.</summary>
    public bool Offline { get; set; }

    /// <summary>The profile the stub currently holds — what a <c>GET</c> returns and a <c>PUT</c> updates.</summary>
    public ProfileResponse Profile { get; set; } = new(Guid.Empty, "Name", "user@example.test", "de-CH", true);

    /// <summary>How many <c>PUT /profile</c> calls have reached the server.</summary>
    public int PutCount { get; private set; }

    /// <summary>The last values a <c>PUT</c> carried, or <see langword="null"/> if none has.</summary>
    public UpdateProfileRequest? LastPut { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (Offline)
        {
            throw new HttpRequestException("The stub is offline.");
        }

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        if (path != ProfilePath)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.Method == HttpMethod.Put)
        {
            PutCount++;
            LastPut = await request.Content!.ReadFromJsonAsync<UpdateProfileRequest>(_json, cancellationToken);
            Profile = Profile with { DisplayName = LastPut!.DisplayName, Language = LastPut.Language };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(Profile, _json), Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>An <see cref="AuthenticationStateProvider"/> that reports one signed-in account.</summary>
/// <param name="accountId">The account id the principal carries.</param>
public sealed class StubAuthStateProvider(Guid accountId) : AuthenticationStateProvider
{
    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}

/// <summary>An <see cref="ICultureProvider"/> that records the last culture it was asked to apply.</summary>
/// <param name="initial">The culture in effect at the start.</param>
public sealed class RecordingCultureProvider(string initial = "de-CH") : ICultureProvider
{
    /// <inheritdoc />
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo(initial);

    /// <inheritdoc />
    public CultureInfo FormattingCulture { get; } = CultureInfo.CurrentCulture;

    /// <summary>The last name passed to <see cref="SetCultureAsync"/>, or <see langword="null"/>.</summary>
    public string? LastSet { get; private set; }

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public ValueTask<CultureInfo> InitializeAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(CurrentCulture);

    /// <inheritdoc />
    public ValueTask SetCultureAsync(string name, CancellationToken cancellationToken = default)
    {
        LastSet = name;
        CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureChanged?.Invoke(this, EventArgs.Empty);
        return ValueTask.CompletedTask;
    }
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> with no browser behind it.</summary>
public sealed class FakeKeyValueStore : IKeyValueStore
{
    private readonly Dictionary<(StorageScope Scope, string Key), string> _values = [];

    /// <inheritdoc />
    public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_values.TryGetValue((scope, key), out var value) ? value : null);

    /// <inheritdoc />
    public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default)
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
