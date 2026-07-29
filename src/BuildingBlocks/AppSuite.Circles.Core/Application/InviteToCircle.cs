using AppSuite.Application;
using AppSuite.Circles.Contracts;
using AppSuite.Circles.Core.Domain;
using Microsoft.Extensions.Options;

namespace AppSuite.Circles.Core.Application;

/// <summary>Command to invite an address to a circle (FR-017). Owner and admin.</summary>
/// <param name="UserId">The inviter.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="Email">The address to invite.</param>
/// <param name="ManagedMembershipId">The managed profile being promoted, when there is one.</param>
public sealed record InviteToCircleCommand(Guid UserId, Guid CircleId, string Email, Guid? ManagedMembershipId)
    : ICommand<CircleResult>;

/// <summary>Handles <see cref="InviteToCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="mailer">The invitation mailer port.</param>
/// <param name="rateLimiter">The per-inviter and per-recipient limits.</param>
/// <param name="limits">The configured caps and lifetimes.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// Every successful outcome here is the same outcome — "invitation sent" — whether a mail went out
/// or not. The three cases that send nothing (the address is already in the circle, it declined
/// recently, or an invitation was already pending and is merely re-issued) are the exact cases whose
/// visibility would answer "does this address have an account?" or "did they say no?".
/// <para>
/// The work is kept as even as it can be across those branches: the same two directory lookups, the
/// same save. The one remaining difference — a queued mail is one row that a suppressed invite does
/// not write — is bounded by <see cref="IInvitationMailer"/> never sending inline, which is what
/// keeps the difference in the noise rather than in the seconds.
/// </para>
/// </remarks>
public sealed class InviteToCircleCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IInvitationMailer mailer,
    IInvitationRateLimiter rateLimiter,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<InviteToCircleCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(InviteToCircleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var permission = circle.Authorize(command.UserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        if (!EmailAddress.IsValid(command.Email))
        {
            return CircleResult.Refused(CircleError.EmailInvalid);
        }

        var inviter = await accounts.FindAsync(command.UserId, cancellationToken);
        if (inviter is null)
        {
            return CircleResult.Refused(CircleError.AccountNotFound);
        }

        var options = limits.Value;
        var emailHash = EmailAddress.HashOf(command.Email);

        var allowance = await rateLimiter.TryConsumeAsync(command.UserId, emailHash, options, cancellationToken);
        if (!allowance.IsAllowed)
        {
            return CircleResult.Refused(CircleError.InvitationRateLimitReached);
        }

        var recipient = await accounts.FindByVerifiedEmailAsync(command.Email, cancellationToken);
        var (secret, handle) = InvitationSecret.Create();

        var dispatch = circle.Invite(
            command.UserId,
            command.Email,
            recipient?.UserId,
            command.ManagedMembershipId,
            handle,
            options,
            timeProvider.GetUtcNow());

        if (!dispatch.IsSuccess)
        {
            return CircleResult.Refused(dispatch.Error!.Value);
        }

        await repository.SaveAsync(circle, cancellationToken);

        if (dispatch.Value.Invitation is not null)
        {
            await mailer.QueueAsync(
                new InvitationMail(
                    EmailAddress.Normalize(command.Email),
                    recipient?.Language ?? inviter.Language,
                    circle.Name,
                    circle.Label,
                    inviter.DisplayName,
                    secret,
                    recipient is not null),
                cancellationToken);
        }

        return CircleResult.Success;
    }
}

/// <summary>Query for a circle's pending invitations (FR-017). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
public sealed record GetPendingInvitationsQuery(Guid UserId, Guid CircleId)
    : IQuery<CircleResult<IReadOnlyList<PendingInvitationResponse>>>;

/// <summary>Handles <see cref="GetPendingInvitationsQuery"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class GetPendingInvitationsQueryHandler(ICircleRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetPendingInvitationsQuery, CircleResult<IReadOnlyList<PendingInvitationResponse>>>
{
    /// <inheritdoc />
    public async Task<CircleResult<IReadOnlyList<PendingInvitationResponse>>> HandleAsync(
        GetPendingInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var circle = await repository.FindAsync(query.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResults.Refused<IReadOnlyList<PendingInvitationResponse>>(CircleError.NotAMember);
        }

        var permission = circle.Authorize(query.UserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return CircleResults.Refused<IReadOnlyList<PendingInvitationResponse>>(permission.Error!.Value);
        }

        return CircleResults.Ok(
            CircleMapper.ToPendingInvitations(circle, timeProvider.GetUtcNow()));
    }
}

/// <summary>Command to re-send a pending invitation (FR-017). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="InvitationId">The invitation to resend.</param>
public sealed record ResendInvitationCommand(Guid UserId, Guid CircleId, Guid InvitationId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="ResendInvitationCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="mailer">The invitation mailer port.</param>
/// <param name="rateLimiter">The per-inviter and per-recipient limits.</param>
/// <param name="limits">The configured caps and lifetimes.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// A resend counts against the same limits as a first invitation. Without that it would be a
/// mail-bombing tool pointed at a third party — which is why the per-recipient limit is keyed on the
/// address rather than on the sender.
/// </remarks>
public sealed class ResendInvitationCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IInvitationMailer mailer,
    IInvitationRateLimiter rateLimiter,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<ResendInvitationCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(ResendInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var permission = circle.Authorize(command.UserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var now = timeProvider.GetUtcNow();
        var invitation = circle.FindInvitation(command.InvitationId);

        if (invitation is null || invitation.IsExpiredAt(now))
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        var inviter = await accounts.FindAsync(command.UserId, cancellationToken);
        if (inviter is null)
        {
            return CircleResult.Refused(CircleError.AccountNotFound);
        }

        var options = limits.Value;

        var allowance = await rateLimiter.TryConsumeAsync(
            command.UserId,
            invitation.EmailHash,
            options,
            cancellationToken);

        if (!allowance.IsAllowed)
        {
            return CircleResult.Refused(CircleError.InvitationRateLimitReached);
        }

        var recipient = await accounts.FindByVerifiedEmailAsync(invitation.Email, cancellationToken);
        var (secret, handle) = InvitationSecret.Create();

        var resent = circle.Resend(command.UserId, command.InvitationId, handle, options, now);
        if (!resent.IsSuccess)
        {
            return CircleResult.Refused(resent.Error!.Value);
        }

        await repository.SaveAsync(circle, cancellationToken);

        await mailer.QueueAsync(
            new InvitationMail(
                resent.Value.Email,
                recipient?.Language ?? inviter.Language,
                circle.Name,
                circle.Label,
                inviter.DisplayName,
                secret,
                recipient is not null),
            cancellationToken);

        return CircleResult.Success;
    }
}

/// <summary>Command to revoke a pending invitation (FR-017). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="InvitationId">The invitation to revoke.</param>
public sealed record RevokeInvitationCommand(Guid UserId, Guid CircleId, Guid InvitationId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="RevokeInvitationCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class RevokeInvitationCommandHandler(ICircleRepository repository)
    : ICommandHandler<RevokeInvitationCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var revoked = circle.Revoke(command.UserId, command.InvitationId);
        if (!revoked.IsSuccess)
        {
            return revoked;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}
