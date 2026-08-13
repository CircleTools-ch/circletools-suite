using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.Circles.Core.Domain;
using Microsoft.Extensions.Options;

namespace CircleTools.Circles.Core.Application;

/// <summary>Command to create a circle (FR-016).</summary>
/// <param name="UserId">The creator, who becomes the sole owner.</param>
/// <param name="Name">The circle's name. Required.</param>
/// <param name="Label">The label; blank falls back to the creator's language default.</param>
public sealed record CreateCircleCommand(Guid UserId, string Name, string? Label)
    : ICommand<CircleResult<CircleDetailResponse>>;

/// <summary>Handles <see cref="CreateCircleCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="accounts">The account directory port.</param>
/// <param name="limits">The configured caps.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class CreateCircleCommandHandler(
    ICircleRepository repository,
    IAccountDirectory accounts,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<CreateCircleCommand, CircleResult<CircleDetailResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CircleDetailResponse>> HandleAsync(
        CreateCircleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindAsync(command.UserId, cancellationToken);
        if (account is null)
        {
            return CircleResults.Refused<CircleDetailResponse>(CircleError.AccountNotFound);
        }

        var now = timeProvider.GetUtcNow();
        var options = limits.Value;

        var createdToday = await repository.CountCreatedSinceAsync(
            command.UserId,
            now - TimeSpan.FromDays(1),
            cancellationToken);

        if (createdToday >= options.CircleCreationsPerDay)
        {
            return CircleResults.Refused<CircleDetailResponse>(CircleError.CircleCreationRateLimitReached);
        }

        var owned = await repository.CountOwnedByAsync(command.UserId, cancellationToken);

        var created = Circle.Create(
            command.UserId,
            account.DisplayName,
            account.Language,
            command.Name,
            command.Label,
            owned,
            options,
            now);

        if (!created.IsSuccess)
        {
            return CircleResults.Refused<CircleDetailResponse>(created.Error!.Value);
        }

        await repository.AddAsync(created.Value, cancellationToken);

        return CircleResults.Ok(CircleMapper.ToDetail(created.Value, command.UserId));
    }
}
