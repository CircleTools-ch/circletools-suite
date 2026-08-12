using AppSuite.Localization;
using AppSuite.Ui.Account.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppSuite.SecretSanta.Ui;

/// <summary>Registers the Secret-Santa UI's shared services in a host's composition root.</summary>
public static class SecretSantaUiServiceCollectionExtensions
{
    /// <summary>The key the chosen language is remembered under, per app (ADR-0011).</summary>
    /// <remarks>
    /// <c>secretsanta.culture</c> and not <c>main.culture</c>: this is the key the shipped MVP has
    /// been writing since before the shared localization block existed. It is user data, so it
    /// survives the migration onto <see cref="AppSuite.Localization"/> unchanged — changing it
    /// would silently reset the language for everyone who has already chosen one (ADR-0025).
    /// </remarks>
    public const string CultureStorageKey = "secretsanta.culture";

    /// <summary>
    /// Adds the suite localization services under Secret-Santa's own storage key.
    /// </summary>
    /// <remarks>
    /// The host must still register an <see cref="AppSuite.Ui.Abstractions.IKeyValueStore"/> — the
    /// browser and the device disagree about what storage is, which is the whole reason it is a
    /// seam.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAppSuiteLocalization(CultureStorageKey);

        // Replace the account block's default online profile writer with the offline-capable one
        // (ADR-0035): a profile edit made offline is queued locally and PUT-flushed on the next sync,
        // which the SyncCoordinator runs alongside the group cycle.
        services.AddScoped<OfflineProfileWriter>();
        services.Replace(ServiceDescriptor.Scoped<IProfileWriter>(sp => sp.GetRequiredService<OfflineProfileWriter>()));
        services.AddScoped<SyncCoordinator>();

        // M4 linking (FEAT-005): the resolution applier and assignment publisher the SyncCoordinator
        // runs, and the create-from-circle service the picker uses. The host must still register the
        // ILinkingApi and ICircleDirectoryApi ports over its authorized HttpClient.
        services.AddScoped<LinkResolutionApplier>();
        services.AddScoped<AssignmentPublisher>();
        services.AddScoped<CreateFromCircleService>();

        return services;
    }
}
