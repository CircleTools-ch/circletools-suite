using CircleTools.Application;
using CircleTools.SecretSanta.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Guards the manual DI wiring in <see cref="SecretSantaCoreServiceCollectionExtensions"/>: every
/// command and query handler in the Secret-Santa core assembly must have a registration. Handlers here are
/// registered by hand, one line each (no assembly scanning), so a new handler is easy to forget — which is
/// exactly how <c>GetRevealSlicesQueryHandler</c> shipped unregistered, silently disabling backend-backed
/// live reveal links (ADR-0068, FEAT-020): the dispatcher threw on send and the publisher's best-effort
/// catch swallowed it. A missing registration otherwise only surfaces at runtime, so this test fails the
/// build the moment a handler has no registration.
/// </summary>
public sealed class SecretSantaHandlerRegistrationTests
{
    private static readonly Type[] HandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    [Fact]
    public void Every_command_and_query_handler_in_the_core_is_registered()
    {
        var services = new ServiceCollection();
        services.AddSecretSantaCore();
        var registeredServiceTypes = services.Select(descriptor => descriptor.ServiceType).ToHashSet();

        var handlerAssembly = typeof(SecretSantaCoreServiceCollectionExtensions).Assembly;

        var missing =
            (from type in handlerAssembly.GetTypes()
             where type is { IsClass: true, IsAbstract: false }
             from handlerInterface in type.GetInterfaces()
             where handlerInterface.IsGenericType
                   && HandlerInterfaceDefinitions.Contains(handlerInterface.GetGenericTypeDefinition())
             where !registeredServiceTypes.Contains(handlerInterface)
             select $"{handlerInterface} -> {type.Name}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These Secret-Santa core handlers are not registered in AddSecretSantaCore " +
            "(register each one explicitly — this is how the reveal-slices handler was missed):\n" +
            string.Join("\n", missing));
    }
}
