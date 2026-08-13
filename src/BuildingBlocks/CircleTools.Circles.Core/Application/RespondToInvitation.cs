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
        CircleLimits limits,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accepted = circle.Accept(
            invitationId,
            account.UserId,
            account.Email,
            account.DisplayName,
            account.Language,
            limits,
            now);

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
public sealed class GetJoinRequestsQueryHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
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
        var emailHash = EmailAddress.HashOf(account.Email);
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
public sealed record AcceptInvitationCommand(Guid UserId, string Secret)
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
public sealed class AcceptInvitationCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
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
            limits.Value,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

/// <summary>Command to accept a join request in the app, with no token from the mail (FR-018).</summary>
/// <param name="UserId">The signed-in, verified caller.</param>
/// <param name="CircleId">The circle being joined.</param>
/// <param name="InvitationId">The invitation being accepted.</param>
public sealed record AcceptJoinRequestCommand(Guid UserId, Guid CircleId, Guid InvitationId)
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
public sealed class AcceptJoinRequestCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
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
            limits.Value,
            timeProvider.GetUtcNow(),
            cancellationToken);
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
public sealed class DeclineJoinRequestCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
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

        if (!string.Equals(invitation.EmailHash, EmailAddress.HashOf(account.Email), StringComparison.Ordinal))
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
