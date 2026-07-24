using AppSuite.Application;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Application;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Reveal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppSuite.SecretSanta.Core;

/// <summary>Dependency-injection registration for the Secret-Santa module's core (domain + application).</summary>
public static class SecretSantaCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Secret-Santa command/query handlers, the draw generator and its randomness
    /// source, the reveal codec/link builder, and a default <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddSecretSantaCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // Draw generation.
        services.TryAddSingleton<IRandomSource, SystemRandomSource>();
        services.AddSingleton<DrawGenerator>();

        // Reveal-link encoding. The host supplies RevealLinkOptions (base URL); default is empty.
        services.TryAddSingleton(new RevealLinkOptions(string.Empty));
        services.AddSingleton<RevealLinkCodec>();
        services.AddSingleton<RevealLinkBuilder>();

        // Command handlers.
        services.AddScoped<ICommandHandler<CreateGroupCommand, Guid>, CreateGroupCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteGroupCommand>, DeleteGroupCommandHandler>();
        services.AddScoped<ICommandHandler<RenameGroupCommand>, RenameGroupCommandHandler>();
        services.AddScoped<ICommandHandler<SetBudgetCommand>, SetBudgetCommandHandler>();
        services.AddScoped<ICommandHandler<SetEventDetailsCommand>, SetEventDetailsCommandHandler>();
        services.AddScoped<ICommandHandler<AddMemberCommand, Guid>, AddMemberCommandHandler>();
        services.AddScoped<ICommandHandler<EditMemberCommand>, EditMemberCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveMemberCommand>, RemoveMemberCommandHandler>();
        services.AddScoped<ICommandHandler<AddExclusionCommand>, AddExclusionCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveExclusionCommand>, RemoveExclusionCommandHandler>();
        services.AddScoped<ICommandHandler<RunDrawCommand, RunDrawResultDto>, RunDrawCommandHandler>();

        // Query handlers.
        services.AddScoped<IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupSummaryDto>>, GetGroupsQueryHandler>();
        services.AddScoped<IQueryHandler<GetGroupQuery, GroupDto?>, GetGroupQueryHandler>();
        services.AddScoped<IQueryHandler<GetRevealLinksQuery, IReadOnlyList<RevealLinkDto>>, GetRevealLinksQueryHandler>();

        return services;
    }
}
