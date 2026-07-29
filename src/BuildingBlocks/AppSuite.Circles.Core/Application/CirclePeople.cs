using AppSuite.Application;
using AppSuite.Circles.Core.Domain;

namespace AppSuite.Circles.Core.Application;

/// <summary>Command to remove someone from a circle (FR-020). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="MembershipId">The membership to remove.</param>
public sealed record RemoveFromCircleCommand(Guid UserId, Guid CircleId, Guid MembershipId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="RemoveFromCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <remarks>
/// The removed person is not mailed. Removal is a social act with no reply channel, and mailing it
/// would make the product a participant in the argument; the circle simply stops being in their
/// list, and an open page says so on their next request.
/// </remarks>
public sealed class RemoveFromCircleCommandHandler(ICircleRepository repository)
    : ICommandHandler<RemoveFromCircleCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(
        RemoveFromCircleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var removed = circle.Remove(command.UserId, command.MembershipId);
        if (!removed.IsSuccess)
        {
            return removed;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}

/// <summary>Command to change someone's role between member and admin (FR-020). Owner only.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="MembershipId">The membership whose role changes.</param>
/// <param name="Role">The new role: <c>admin</c> or <c>member</c>.</param>
public sealed record ChangeMemberRoleCommand(Guid UserId, Guid CircleId, Guid MembershipId, string Role)
    : ICommand<CircleResult>;

/// <summary>Handles <see cref="ChangeMemberRoleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class ChangeMemberRoleCommandHandler(ICircleRepository repository)
    : ICommandHandler<ChangeMemberRoleCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(
        ChangeMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        // "owner" does not parse, so asking for it lands on the same refusal as asking for nonsense:
        // ownership moves by transfer, never by a role change.
        var role = CircleMapper.ParseRole(command.Role);
        if (role is null)
        {
            var permission = circle.Authorize(command.UserId, CircleRole.Owner);

            return permission.IsSuccess
                ? CircleResult.Refused(CircleError.OwnershipRequiresTransfer)
                : permission;
        }

        var changed = circle.ChangeRole(command.UserId, command.MembershipId, role.Value);
        if (!changed.IsSuccess)
        {
            return changed;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}

/// <summary>Command to transfer ownership (FR-020). Owner only, immediate.</summary>
/// <param name="UserId">The current owner.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="MembershipId">The membership receiving ownership.</param>
public sealed record TransferOwnershipCommand(Guid UserId, Guid CircleId, Guid MembershipId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="TransferOwnershipCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <remarks>
/// The whole transfer is one change to one aggregate, so it cannot half-happen. If the target's
/// membership ended concurrently, the save loses the optimistic-concurrency race and the circle
/// keeps its current owner — which is what makes "exactly one owner" checkable rather than hoped for.
/// </remarks>
public sealed class TransferOwnershipCommandHandler(ICircleRepository repository)
    : ICommandHandler<TransferOwnershipCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(
        TransferOwnershipCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var transferred = circle.TransferOwnership(command.UserId, command.MembershipId);
        if (!transferred.IsSuccess)
        {
            return transferred;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}

/// <summary>Command to leave a circle (FR-021).</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle to leave.</param>
public sealed record LeaveCircleCommand(Guid UserId, Guid CircleId) : ICommand<CircleResult>;

/// <summary>Handles <see cref="LeaveCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <remarks>
/// Leaving needs nobody's permission — requiring an admin to release someone from a group they no
/// longer want to be in makes the product an instrument in the argument. The owner is the exception,
/// and the refusal names which way out applies so the UI can offer it inline rather than dead-ending.
/// </remarks>
public sealed class LeaveCircleCommandHandler(ICircleRepository repository)
    : ICommandHandler<LeaveCircleCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(LeaveCircleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var left = circle.Leave(command.UserId);
        if (!left.IsSuccess)
        {
            return left;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}
