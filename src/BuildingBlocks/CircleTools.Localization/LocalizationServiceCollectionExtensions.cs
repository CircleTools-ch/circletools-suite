using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Localization;

/// <summary>Registers the suite's localization services in a host's composition root.</summary>
public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Adds RESX localization (ADR-0011) and the <see cref="ICultureProvider"/> that owns the chosen
    /// language.
    /// </summary>
    /// <remarks>
    /// The host must still register an <see cref="IKeyValueStore"/>; this method does not, because the
    /// storage medium is exactly the part that differs between a browser and a device.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="storageKey">The key the chosen culture is remembered under, per app.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCircleToolsLocalization(this IServiceCollection services, string storageKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSingleton(new CultureOptions(storageKey));

        // Scoped, not singleton: it depends on IKeyValueStore, which a host registers scoped like
        // everything else in a Blazor app. A singleton holding a scoped dependency is a captive
        // dependency, and scope validation rejects it at startup - correctly, even though a WASM
        // client only ever has one scope.
        services.AddScoped<ICultureProvider, CultureProvider>();

        return services;
    }
}
