using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Reveal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CircleTools.SecretSanta.Core;

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
        services.AddScoped<ICommandHandler<CopyGroupCommand, Guid>, CopyGroupCommandHandler>();
        services.AddScoped<ICommandHandler<CreateGroupFromCircleCommand, CreateGroupFromCircleResult>, CreateGroupFromCircleCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteGroupCommand>, DeleteGroupCommandHandler>();
        services.AddScoped<ICommandHandler<RenameGroupCommand>, RenameGroupCommandHandler>();
        services.AddScoped<ICommandHandler<SetBudgetCommand>, SetBudgetCommandHandler>();
        services.AddScoped<ICommandHandler<SetEventDetailsCommand>, SetEventDetailsCommandHandler>();
        services.AddScoped<ICommandHandler<AddMemberCommand, Guid>, AddMemberCommandHandler>();
        services.AddScoped<ICommandHandler<EditMemberCommand>, EditMemberCommandHandler>();
        services.AddScoped<ICommandHandler<SetMemberGiftNoteCommand>, SetMemberGiftNoteCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveMemberCommand>, RemoveMemberCommandHandler>();
        services.AddScoped<ICommandHandler<AddExclusionCommand>, AddExclusionCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveExclusionCommand>, RemoveExclusionCommandHandler>();
        services.AddScoped<ICommandHandler<RunDrawCommand, RunDrawResultDto>, RunDrawCommandHandler>();
        services.AddScoped<ICommandHandler<SetMemberWishlistCommand>, SetMemberWishlistCommandHandler>();

        // Event (occasion) handlers (ADR-0042).
        services.AddScoped<ICommandHandler<AddEventCommand, Guid>, AddEventCommandHandler>();
        services.AddScoped<ICommandHandler<RenameEventCommand>, RenameEventCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveEventCommand>, RemoveEventCommandHandler>();

        // Query handlers.
        services.AddScoped<IQueryHandler<GetGroupsQuery, IReadOnlyList<GroupSummaryDto>>, GetGroupsQueryHandler>();
        services.AddScoped<IQueryHandler<GetGroupQuery, GroupDto?>, GetGroupQueryHandler>();
        services.AddScoped<IQueryHandler<CheckDrawFeasibilityQuery, DrawFeasibilityDto?>, CheckDrawFeasibilityQueryHandler>();
        services.AddScoped<IQueryHandler<GetEventQuery, EventDto?>, GetEventQueryHandler>();
        services.AddScoped<IQueryHandler<GetRevealLinksQuery, IReadOnlyList<RevealLinkDto>>, GetRevealLinksQueryHandler>();
        services.AddScoped<IQueryHandler<GetRevealSlicesQuery, IReadOnlyList<RevealSliceData>>, GetRevealSlicesQueryHandler>();

        return services;
    }
}
