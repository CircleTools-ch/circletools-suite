using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Application;

/// <summary>Command to edit a circle's name and label (FR-020). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="Name">The new name. Required.</param>
/// <param name="Label">The new label; blank keeps the stored one.</param>
public sealed record UpdateCircleCommand(Guid UserId, Guid CircleId, string Name, string? Label)
    : ICommand<CircleResult<CircleDetailResponse>>;

/// <summary>Handles <see cref="UpdateCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class UpdateCircleCommandHandler(ICircleRepository repository)
    : ICommandHandler<UpdateCircleCommand, CircleResult<CircleDetailResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CircleDetailResponse>> HandleAsync(
        UpdateCircleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResults.Refused<CircleDetailResponse>(CircleError.NotAMember);
        }

        var updated = circle.Update(command.UserId, command.Name, command.Label);
        if (!updated.IsSuccess)
        {
            return CircleResults.Refused<CircleDetailResponse>(updated.Error!.Value);
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResults.Ok(CircleMapper.ToDetail(circle, command.UserId));
    }
}
