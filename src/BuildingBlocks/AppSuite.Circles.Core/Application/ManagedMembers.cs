using AppSuite.Application;
using AppSuite.Circles.Contracts;
using AppSuite.Circles.Core.Domain;
using Microsoft.Extensions.Options;

namespace AppSuite.Circles.Core.Application;

/// <summary>Command to add a managed member (FR-019). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="DisplayName">Their display name. Required, and not unique.</param>
/// <param name="Language">Their preferred language, if known.</param>
public sealed record AddManagedMemberCommand(Guid UserId, Guid CircleId, string DisplayName, string? Language)
    : ICommand<CircleResult<CirclePersonResponse>>;

/// <summary>Handles <see cref="AddManagedMemberCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
/// <param name="limits">The configured caps.</param>
/// <param name="timeProvider">The clock.</param>
/// <remarks>
/// A managed member's profile is a display name and, optionally, a language. Nothing else, and
/// deliberately so: this is personal data about someone — often a child — who did not enter it,
/// cannot review it and cannot ask for it to be corrected, so anything more would need a
/// justification the product does not have (NFR-004).
/// </remarks>
public sealed class AddManagedMemberCommandHandler(
    ICircleRepository repository,
    IOptions<CircleLimits> limits,
    TimeProvider timeProvider)
    : ICommandHandler<AddManagedMemberCommand, CircleResult<CirclePersonResponse>>
{
    /// <inheritdoc />
    public async Task<CircleResult<CirclePersonResponse>> HandleAsync(
        AddManagedMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResults.Refused<CirclePersonResponse>(CircleError.NotAMember);
        }

        var added = circle.AddManagedMember(
            command.UserId,
            command.DisplayName,
            command.Language,
            limits.Value,
            timeProvider.GetUtcNow());

        if (!added.IsSuccess)
        {
            return CircleResults.Refused<CirclePersonResponse>(added.Error!.Value);
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResults.Ok(CircleMapper.ToPerson(added.Value, command.UserId));
    }
}

/// <summary>Command to edit a managed member's profile (FR-019). Owner and admin.</summary>
/// <param name="UserId">The caller.</param>
/// <param name="CircleId">The circle.</param>
/// <param name="MembershipId">The managed membership to edit.</param>
/// <param name="DisplayName">The new display name. Required.</param>
/// <param name="Language">The new preferred language, or <c>null</c> to clear it.</param>
public sealed record UpdateManagedMemberCommand(
    Guid UserId,
    Guid CircleId,
    Guid MembershipId,
    string DisplayName,
    string? Language) : ICommand<CircleResult>;

/// <summary>Handles <see cref="UpdateManagedMemberCommand"/>.</summary>
/// <param name="repository">The circle repository port.</param>
public sealed class UpdateManagedMemberCommandHandler(ICircleRepository repository)
    : ICommandHandler<UpdateManagedMemberCommand, CircleResult>
{
    /// <inheritdoc />
    public async Task<CircleResult> HandleAsync(
        UpdateManagedMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var circle = await repository.FindAsync(command.CircleId, cancellationToken);
        if (circle is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        var updated = circle.UpdateManagedMember(
            command.UserId,
            command.MembershipId,
            command.DisplayName,
            command.Language);

        if (!updated.IsSuccess)
        {
            return updated;
        }

        await repository.SaveAsync(circle, cancellationToken);

        return CircleResult.Success;
    }
}
