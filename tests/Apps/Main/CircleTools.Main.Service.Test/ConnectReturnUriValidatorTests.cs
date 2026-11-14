// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Main.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CircleTools.Main.Service.Test;

/// <summary>
/// Unit coverage of the SSO <c>return_uri</c> allow-list (FR-031, ADR-0039, TP-006).
/// </summary>
/// <remarks>
/// The load-bearing property is that the allow-list is sourced from the <em>same</em>
/// <c>Cors:AllowedOrigins</c> configuration the CORS policy reads (ADR-0013): a second, hand-kept list
/// is exactly the divergence a code-exfiltration hole hides in. These tests pin that behaviour — an
/// origin present in that configuration is honoured, one absent from it is refused — plus the
/// open-redirect guards (absolute-URI only, origin compared, loopback only in Development).
/// </remarks>
public sealed class ConnectReturnUriValidatorTests
{
    private const string AllowedOrigin = "https://secretsanta.circletools.ch";

    private static ConnectReturnUriValidator Validator(bool development = false, params string[] origins)
    {
        var settings = origins
            .Select((origin, index) => new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin));

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ConnectReturnUriValidator(configuration, new StubEnvironment(development));
    }

    [Fact]
    public void AnOriginPresentInTheCorsConfiguration_IsAllowed()
    {
        var validator = Validator(origins: AllowedOrigin);

        Assert.True(validator.IsAllowed($"{AllowedOrigin}/account/connect/callback"));
    }

    [Fact]
    public void AnOriginAbsentFromTheCorsConfiguration_IsRefused()
    {
        var validator = Validator(origins: AllowedOrigin);

        Assert.False(validator.IsAllowed("https://evil.example/steal"));
    }

    [Fact]
    public void ANonAbsoluteOrEmptyReturnUri_IsRefused()
    {
        var validator = Validator(origins: AllowedOrigin);

        Assert.False(validator.IsAllowed("/account/connect/callback"));
        Assert.False(validator.IsAllowed(null));
        Assert.False(validator.IsAllowed("   "));
    }

    [Fact]
    public void APortOrSchemeMismatchAgainstAnAllowedOrigin_IsRefused()
    {
        var validator = Validator(origins: AllowedOrigin);

        // Same host, different origin: the guard compares scheme + host + port, not a prefix.
        Assert.False(validator.IsAllowed("http://secretsanta.circletools.ch/account/connect/callback"));
        Assert.False(validator.IsAllowed("https://secretsanta.circletools.ch:8443/account/connect/callback"));
    }

    [Fact]
    public void Loopback_IsAllowedOnlyInDevelopment()
    {
        // Aspire assigns proxy ports at start-up that cannot be pinned in configuration, so Development
        // trusts loopback the same way the CORS policy does.
        Assert.True(Validator(development: true).IsAllowed("http://localhost:5173/account/connect/callback"));
        Assert.False(Validator(development: false).IsAllowed("http://localhost:5173/account/connect/callback"));
    }

    private sealed class StubEnvironment(bool development) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = development ? Environments.Development : Environments.Production;
        public string ApplicationName { get; set; } = "CircleTools.Main.Service.Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
