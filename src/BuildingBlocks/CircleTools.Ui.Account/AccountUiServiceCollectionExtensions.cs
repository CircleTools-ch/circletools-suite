using CircleTools.ApiClient;
using CircleTools.Ui.Account.Api;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.Ui.Account;

/// <summary>Registers the shared account/session services in a host's composition root (ADR-0034).</summary>
public static class AccountUiServiceCollectionExtensions
{
    /// <summary>
    /// Adds the account API client, the token stores, the authentication state provider and the
    /// session service — everything the account pages need, minus the parts a host supplies.
    /// </summary>
    /// <remarks>
    /// The host must still register an <see cref="CircleTools.Ui.Abstractions.IKeyValueStore"/>, an
    /// <see cref="CircleTools.Ui.Abstractions.IPasskeyCeremony"/>, an <c>ICultureProvider</c> (via
    /// <c>AddCircleToolsLocalization</c>), and the two keyed <see cref="HttpClient"/>s named in
    /// <see cref="AccountApiClients"/> — those are the parts that differ between a browser and a
    /// device, which is the whole reason they are seams.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="refreshTokenStorageKey">
    /// The per-app key the refresh token is remembered under (ADR-0026). Each app passes its own so a
    /// device that hosts more than one never crosses their sessions.
    /// </param>
    /// <param name="appId">
    /// This host's stable source-app id (ADR-0064), e.g. <c>secretsanta</c> or <c>circletools</c>. It is
    /// stamped onto the requests that trigger mail so it is branded — and links back — to the app the
    /// user used. Empty falls back to <c>circletools</c> (the default branding).
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAccountUi(
        this IServiceCollection services,
        string refreshTokenStorageKey,
        string? appId = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenStorageKey);

        services.AddAuthorizationCore();

        services.AddSingleton(new AccountAppContext(string.IsNullOrWhiteSpace(appId) ? "circletools" : appId));

        services.AddScoped<IAccessTokenStore>(_ => new InMemoryAccessTokenStore(TimeProvider.System));
        services.AddSingleton(new RefreshTokenStoreOptions(refreshTokenStorageKey));
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        // Registered twice on purpose: AuthorizeView resolves the base type, while the session
        // classes need the concrete one to announce a change. Both must be the same instance.
        services.AddScoped<TokenAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthStateProvider>());

        services.AddScoped<SessionNotices>();
        services.AddScoped<ISessionRefresher, SessionRefresher>();
        services.AddScoped<AccountApiClient>();
        services.AddScoped<AccountSession>();

        // The default is the direct-online writer (ADR-0035): a profile edit is a PUT, which is the
        // whole behaviour for a host with no offline story. TryAdd so a host that works offline can
        // register its own queueing writer instead — the Secret-Santa app does exactly that.
        services.TryAddScoped<IProfileWriter, OnlineProfileWriter>();

        return services;
    }

    /// <summary>
    /// Adds the cross-app single-sign-on hand-off (FR-031, ADR-0039). Opt-in, on top of
    /// <see cref="AddAccountUi"/>.
    /// </summary>
    /// <remarks>
    /// Requires the host's <c>NavigationManager</c> (every Blazor host has one) and the
    /// <see cref="CircleTools.Ui.Abstractions.IKeyValueStore"/> already registered for the refresh
    /// token. The authority origin and client id come from the host's own configuration, so an app
    /// that never starts a hand-off carries an inert value (NFR-001).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="authorityBaseUrl">The SSO authority origin (e.g. the Main shell).</param>
    /// <param name="clientId">A stable identifier for this app.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAccountSso(
        this IServiceCollection services,
        string authorityBaseUrl,
        string clientId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        services.AddSingleton(new SsoConnectOptions(authorityBaseUrl, clientId));
        services.AddScoped<SsoConnect>();

        return services;
    }
}
