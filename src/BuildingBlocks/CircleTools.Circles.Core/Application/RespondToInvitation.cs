// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Domain;
using Microsoft.Extensions.Options;

namespace CircleTools.Circles.Core.Application;

/// <summary>
/// The one accept path (FR-018). Both branches — the mailed link and the in-app join request —
/// come through here, so the rule that decides everything, <em>the caller's verified address is the
/// invited address</em>, is applied in a single place and cannot drift apart between them.
/// </summary>
internal static class InvitationResponse
{
    public static async Task<CircleResult<CircleSummaryResponse>> AcceptAsync(
        ICircleRepository repository,
        Circle circle,
        Guid invitationId,
        CircleAccount account,
        IEmailHasher emailHasher,
        CircleLimits limits,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? chosenManagedMembershipId = null)
    {
        var accepted = circle.Accept(
            invitationId,
            account.UserId,
            emailHasher.Hash(account.Email),
            account.DisplayName,
            account.Language,
            limits,
            now,
            chosenManagedMembershipId);

        // Saved on every outcome, not only the successful one: an expired invitation is dropped and
        // an invitation accepted by someone already in the circle is consumed, and both of those are
        // state changes that must not be lost just because the answer was a refusal.
        await repository.SaveAsync(circle, cancellationToken);

        return accepted.IsSuccess
            ? CircleResults.Ok(CircleMapper.ToSummary(circle, account.UserId))
            : CircleResults.Refused<CircleSummaryResponse>(accepted.Error!.Value);
    }

    public static async Task<CircleResult> DeclineAsync(
        ICircleRepository repository,
        Circle circle,
        Guid invitationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var declined = circle.Decline(invitationId, now);
        if (!declined.IsSuccess)
        {
            return declined;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}

/// <summary>
/// Query for what the holder of an invitation link may see before answering (FR-018). Anonymous.
/// </summary>
/// <param name="Secret">The secret from the link.</param>
public sealed record GetInvitationPreviewQuery(string Secret) : IQuery<CircleResult<InvitationPreviewResponse>>;

/// <summary>Handles <see cref="GetInvitationPreviewQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// The preview shows the circle's name, its label and the inviter's display name, and nothing else —
/// in particular not whether the invited address has an account, which anyone the link was forwarded
/// to would otherwise learn.
/// </remarks>
public sealed class GetInvitationPreviewQueryHandler(ICircleRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetInvitationPreviewQuery, CircleResult<InvitationPreviewResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<InvitationPreviewResponse>> HandleAsync(
        GetInvitationPreviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Secret))
        {
            return CircleResults.Refused<InvitationPreviewResponse>(CircleError.InvitationNotFound);
        }

        var handle = InvitationSecret.HandleFor(query.Secret);
        var circle = await repository.FindByInvitationHandleAsync(handle, cancellationToken);
        var invitation = circle?.FindInvitationByHandle(handle);

        if (circle is null || invitation is null)
        {
            return CircleResults.Refused<InvitationPreviewResponse>(CircleError.InvitationNotFound);
        }

        if (invitation.IsExpiredAt(timeProvider.GetUtcNow()))
        {
            return CircleResults.Refused<InvitationPreviewResponse>(CircleError.InvitationExpired);
        }

        return CircleResults.Ok(
            new InvitationPreviewResponse(
                circle.Name,
                circle.Label,
                CircleMapper.InviterDisplayName(circle, invitation)));
    }
}

/// <summary>Query for the invitations waiting for the caller's verified address (FR-018).</summary>
/// <param name="UserId">The caller.</param>
public sealed record GetJoinRequestsQuery(Guid UserId) : IQuery<IReadOnlyList<JoinRequestResponse>>;

/// <summary>Handles <see cref="GetJoinRequestsQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class GetJoinRequestsQueryHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    TimeProvider timeProvider)
    : IQueryHandler<GetJoinRequestsQuery, IReadOnlyList<JoinRequestResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<JoinRequestResponse>> HandleAsync(
        GetJoinRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await accounts.FindAsync(query.UserId, cancellationToken);
        if (account is null)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var emailHash = emailHasher.Hash(account.Email);
        var circles = await repository.ListWithInvitationForAsync(emailHash, cancellationToken);
        var requests = new List<JoinRequestResponse>();

        foreach (var circle in circles)
        {
            var invitation = circle.Invitations.FirstOrDefault(i =>
                string.Equals(i.EmailHash, emailHash, StringComparison.Ordinal) && !i.IsExpiredAt(now));

            if (invitation is not null)
            {
                requests.Add(new JoinRequestResponse(
                    circle.Id,
                    invitation.Id,
                    circle.Name,
                    circle.Label,
                    CircleMapper.InviterDisplayName(circle, invitation)));
            }
        }

        return requests;
    }
}

/// <summary>Command to accept an invitation from its mailed link (FR-018).</summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="Secret">The secret from the link.</param>
/// <param name="ChosenManagedMembershipId">
/// The offline profile the recipient chose to join as, or null to join as new (FR-019). Honoured only
/// when the inviter did not already aim the invitation at a managed member.
/// </param>
public sealed record AcceptInvitationCommand(Guid UserId, string Secret, Guid? ChosenManagedMembershipId = null)
    : ICommand<CircleResult<CircleSummaryResponse>>;

/// <summary>Handles <see cref="AcceptInvitationCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="limits">The configured caps.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// The link is not a bearer key. Someone who was forwarded it and accepts with their own account
/// creates nothing and learns nothing — the invited address is compared against the caller's
/// <em>verified</em> one, which is the whole of the forwarded-mail defence.
/// </remarks>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class AcceptInvitationCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<AcceptInvitationCommand, CircleResult<CircleSummaryResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CircleSummaryResponse>> HandleAsync(
        AcceptInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindAsync(command.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResults.Refused<CircleSummaryResponse>(CircleError.AccountNotFound);
        }

        if (string.IsNullOrWhiteSpace(command.Secret))
        {
            return CircleResults.Refused<CircleSummaryResponse>(CircleError.InvitationNotFound);
        }

        var handle = InvitationSecret.HandleFor(command.Secret);
        var circle = await repository.FindByInvitationHandleAsync(handle, cancellationToken);
        var invitation = circle?.FindInvitationByHandle(handle);

        if (circle is null || invitation is null)
        {
            return CircleResults.Refused<CircleSummaryResponse>(CircleError.InvitationNotFound);
        }

        return await InvitationResponse.AcceptAsync(
            repository,
            circle,
            invitation.Id,
            account,
            emailHasher,
            limits.Value,
            timeProvider.GetUtcNow(),
            cancellationToken,
            command.ChosenManagedMembershipId);
    }
}

/// <summary>Command to accept a join request in the app, with no token from the mail (FR-018).</summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="CircleId">The circle being joined.</param>
/// <param name="InvitationId">The invitation being accepted.</param>
/// <param name="ChosenManagedMembershipId">
/// The offline profile the recipient chose to join as, or null to join as new (FR-019). Honoured only
/// when the inviter did not already aim the invitation at a managed member.
/// </param>
public sealed record AcceptJoinRequestCommand(
    Guid UserId,
    Guid CircleId,
    Guid InvitationId,
    Guid? ChosenManagedMembershipId = null)
    : ICommand<CircleResult<CircleSummaryResponse>>;

/// <summary>Handles <see cref="AcceptJoinRequestCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="limits">The configured caps.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// Identifying the invitation by its identifier rather than by a secret is safe precisely because
/// the identifier grants nothing: the address check below is what admits anyone, and it is the same
/// check the link path makes.
/// </remarks>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class AcceptJoinRequestCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<AcceptJoinRequestCommand, CircleResult<CircleSummaryResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CircleSummaryResponse>> HandleAsync(
        AcceptJoinRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindAsync(command.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResults.Refused<CircleSummaryResponse>(CircleError.AccountNotFound);
        }

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResults.Refused<CircleSummaryResponse>(CircleError.InvitationNotFound);
        }

        return await InvitationResponse.AcceptAsync(
            repository,
            circle,
            command.InvitationId,
            account,
            emailHasher,
            limits.Value,
            timeProvider.GetUtcNow(),
            cancellationToken,
            command.ChosenManagedMembershipId);
    }
}

/// <summary>Command to decline an invitation from its mailed link (FR-018). Anonymous.</summary>
/// <param name="Secret">The secret from the link.</param>
public sealed record DeclineInvitationCommand(string Secret) : ICommand<CircleResult>;

/// <summary>Handles <see cref="DeclineInvitationCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// Declining deliberately needs no account, and that is a real trade-off: anyone holding a forwarded
/// link can decline an invitation that was not theirs, and the inviter's only recourse is to resend.
/// Requiring an account would be worse — "declined" appearing in the pending list would then prove
/// the address has one, which is the oracle ADR-0018 paid UX to close.
/// </remarks>
public sealed class DeclineInvitationCommandHandler(ICircleRepository repository, TimeProvider timeProvider)
    : ICommandHandler<DeclineInvitationCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(DeclineInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Secret))
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        var handle = InvitationSecret.HandleFor(command.Secret);
        var circle = await repository.FindByInvitationHandleAsync(handle, cancellationToken);
        var invitation = circle?.FindInvitationByHandle(handle);

        if (circle is null || invitation is null)
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        return await InvitationResponse.DeclineAsync(
            repository,
            circle,
            invitation.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

/// <summary>Command to decline a join request in the app (FR-018).</summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="InvitationId">The invitation being declined.</param>
public sealed record DeclineJoinRequestCommand(Guid UserId, Guid CircleId, Guid InvitationId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="DeclineJoinRequestCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// The link path admits anyone holding the link; this path admits nobody but the invited address.
/// Without that check an identifier — which is not a secret — would be enough for any signed-in user
/// to decline someone else's invitation.
/// </remarks>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class DeclineJoinRequestCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    TimeProvider timeProvider)
    : ICommandHandler<DeclineJoinRequestCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(
        DeclineJoinRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindAsync(command.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResult.Refused(CircleError.AccountNotFound);
        }

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        var invitation = circle?.FindInvitation(command.InvitationId);

        if (circle is null || invitation is null)
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        if (!string.Equals(invitation.EmailHash, emailHasher.Hash(account.Email), StringComparison.Ordinal))
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        return await InvitationResponse.DeclineAsync(
            repository,
            circle,
            invitation.Id,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

/// <summary>
/// Query for the offline profiles the recipient of a <em>mailed</em> invitation may join as (FR-019).
/// Signed in, and gated on the caller's verified address being the invited one.
/// </summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="Secret">The secret from the link.</param>
public sealed record GetInvitationTargetsBySecretQuery(Guid UserId, string Secret)
    : IQuery<CircleResult<JoinTargetsResponse>>;

/// <summary>Handles <see cref="GetInvitationTargetsBySecretQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// The offline roster this returns is member-only data (NFR-006), relaxed just far enough to let the
/// invited person say who they are (ADR-0043). The relaxation is the address check below: a forwarded
/// link resolves the same as an unknown one, so a link-holder who is not the invited address learns
/// nothing — not the roster, and not whether the address has an account.
/// </remarks>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class GetInvitationTargetsBySecretQueryHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    TimeProvider timeProvider)
    : IQueryHandler<GetInvitationTargetsBySecretQuery, CircleResult<JoinTargetsResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<JoinTargetsResponse>> HandleAsync(
        GetInvitationTargetsBySecretQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await accounts.FindAsync(query.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResults.Refused<JoinTargetsResponse>(CircleError.AccountNotFound);
        }

        if (string.IsNullOrWhiteSpace(query.Secret))
        {
            return CircleResults.Refused<JoinTargetsResponse>(CircleError.InvitationNotFound);
        }

        var handle = InvitationSecret.HandleFor(query.Secret);
        var circle = await repository.FindByInvitationHandleAsync(handle, cancellationToken);
        var invitation = circle?.FindInvitationByHandle(handle);

        return Resolve(circle, invitation, emailHasher.Hash(account.Email), timeProvider.GetUtcNow());
    }

    internal static CircleResult<JoinTargetsResponse> Resolve(
        Circle? circle,
        Invitation? invitation,
        string accountEmailHash,
        DateTimeOffset now)
    {
        if (circle is null || invitation is null || invitation.IsExpiredAt(now))
        {
            return CircleResults.Refused<JoinTargetsResponse>(CircleError.InvitationNotFound);
        }

        // The whole of the forwarded-link defence: only the invited address is answered, and a
        // mismatch is told exactly what an unknown invitation is told.
        if (!string.Equals(invitation.EmailHash, accountEmailHash, StringComparison.Ordinal))
        {
            return CircleResults.Refused<JoinTargetsResponse>(CircleError.InvitationNotFound);
        }

        return CircleResults.Ok(CircleMapper.ToJoinTargets(circle, invitation));
    }
}

/// <summary>
/// Query for the offline profiles the recipient of an <em>in-app</em> join request may join as
/// (FR-019). Same address gate as the mailed path, addressed by circle and invitation id.
/// </summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="InvitationId">The invitation.</param>
public sealed record GetJoinRequestTargetsQuery(Guid UserId, Guid CircleId, Guid InvitationId)
    : IQuery<CircleResult<JoinTargetsResponse>>;

/// <summary>Handles <see cref="GetJoinRequestTargetsQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="emailHasher">The keyed email hasher (ADR-0062).</param>
public sealed class GetJoinRequestTargetsQueryHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IEmailHasher emailHasher,
    TimeProvider timeProvider)
    : IQueryHandler<GetJoinRequestTargetsQuery, CircleResult<JoinTargetsResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<JoinTargetsResponse>> HandleAsync(
        GetJoinRequestTargetsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await accounts.FindAsync(query.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResults.Refused<JoinTargetsResponse>(CircleError.AccountNotFound);
        }

        var circle = await repository.FindAsync(query.CircleId, cancellationToken);
        var invitation = circle?.FindInvitation(query.InvitationId);

        return GetInvitationTargetsBySecretQueryHandler.Resolve(
            circle, invitation, emailHasher.Hash(account.Email), timeProvider.GetUtcNow());
    }
}
