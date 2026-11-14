// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Application;
using CircleTools.Circles.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Circles.Core;

/// <summary>Dependency-injection registration for the Circles context's core (domain + application).</summary>
/// <remarks>
/// The ports — <see cref="ICircleRepository"/>, <see cref="IAccountDirectory"/>,
/// <see cref="IInvitationMailer"/> and <see cref="IInvitationRateLimiter"/> — are deliberately not
/// registered here. They are adapters, and the composition root that owns Postgres, Identity and the
/// outbox is the only place that can supply them.
/// </remarks>
public static class CirclesCoreServiceCollectionExtensions
{
    /// <summary>Registers the Circles context's command and query handlers, and its limits.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCirclesCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingletonTimeProvider();
        services.AddOptions<CircleLimits>();

        // Circles (FR-016, FR-020, FR-022)
        services.AddScoped<
            ICommandHandler<CreateCircleCommand, CircleResult<CircleDetailResponse>>,
            CreateCircleCommandHandler>();
        services.AddScoped<
            ICommandHandler<UpdateCircleCommand, CircleResult<CircleDetailResponse>>,
            UpdateCircleCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteCircleCommand, CircleResult>, DeleteCircleCommandHandler>();
        services.AddScoped<
            IQueryHandler<GetMyCirclesQuery, IReadOnlyList<CircleSummaryResponse>>,
            GetMyCirclesQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetCircleQuery, CircleResult<CircleDetailResponse>>,
            GetCircleQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetSoleOwnedCirclesQuery, IReadOnlyList<SoleOwnedCircleResponse>>,
            GetSoleOwnedCirclesQueryHandler>();

        // Invitations (FR-017)
        services.AddScoped<ICommandHandler<InviteToCircleCommand, CircleResult>, InviteToCircleCommandHandler>();
        services.AddScoped<ICommandHandler<ResendInvitationCommand, CircleResult>, ResendInvitationCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeInvitationCommand, CircleResult>, RevokeInvitationCommandHandler>();
        services.AddScoped<
            IQueryHandler<GetPendingInvitationsQuery, CircleResult<IReadOnlyList<PendingInvitationResponse>>>,
            GetPendingInvitationsQueryHandler>();

        // Responding to an invitation (FR-018)
        services.AddScoped<
            IQueryHandler<GetInvitationPreviewQuery, CircleResult<InvitationPreviewResponse>>,
            GetInvitationPreviewQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetJoinRequestsQuery, IReadOnlyList<JoinRequestResponse>>,
            GetJoinRequestsQueryHandler>();
        services.AddScoped<
            ICommandHandler<AcceptInvitationCommand, CircleResult<CircleSummaryResponse>>,
            AcceptInvitationCommandHandler>();
        services.AddScoped<
            ICommandHandler<AcceptJoinRequestCommand, CircleResult<CircleSummaryResponse>>,
            AcceptJoinRequestCommandHandler>();
        services.AddScoped<
            ICommandHandler<DeclineInvitationCommand, CircleResult>,
            DeclineInvitationCommandHandler>();
        services.AddScoped<
            ICommandHandler<DeclineJoinRequestCommand, CircleResult>,
            DeclineJoinRequestCommandHandler>();
        services.AddScoped<
            IQueryHandler<GetInvitationTargetsBySecretQuery, CircleResult<JoinTargetsResponse>>,
            GetInvitationTargetsBySecretQueryHandler>();
        services.AddScoped<
            IQueryHandler<GetJoinRequestTargetsQuery, CircleResult<JoinTargetsResponse>>,
            GetJoinRequestTargetsQueryHandler>();

        // Managed members (FR-019)
        services.AddScoped<
            ICommandHandler<AddManagedMemberCommand, CircleResult<CirclePersonResponse>>,
            AddManagedMemberCommandHandler>();
        services.AddScoped<
            ICommandHandler<UpdateManagedMemberCommand, CircleResult>,
            UpdateManagedMemberCommandHandler>();

        // Roles, membership and leaving (FR-020, FR-021)
        services.AddScoped<ICommandHandler<RemoveFromCircleCommand, CircleResult>, RemoveFromCircleCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeMemberRoleCommand, CircleResult>, ChangeMemberRoleCommandHandler>();
        services.AddScoped<ICommandHandler<TransferOwnershipCommand, CircleResult>, TransferOwnershipCommandHandler>();
        services.AddScoped<ICommandHandler<LeaveCircleCommand, CircleResult>, LeaveCircleCommandHandler>();

        // Account deletion's second seam (FR-015)
        services.AddScoped<ICommandHandler<PurgeAccountFromCirclesCommand>, PurgeAccountFromCirclesCommandHandler>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
