// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Application;

/// <summary>Dependency-injection registration for the application layer.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CQRS <see cref="ISender"/> and the ordered pipeline behaviors.
    /// Individual modules register their own command/query handlers.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ISender, Sender>();

        // Behaviors run in registration order (logging first for the skeleton).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        return services;
    }
}
