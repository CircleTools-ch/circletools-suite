using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>Command to set or clear a member's stable gift note (FR-037).</summary>
/// <param name="GroupId">The owning group.</param>
/// <param name="MemberId">The member whose gift note is set.</param>
/// <param name="GiftNote">The gift note; empty or whitespace clears it.</param>
public sealed record SetMemberGiftNoteCommand(Guid GroupId, Guid MemberId, string? GiftNote) : ICommand;

/// <summary>Handles <see cref="SetMemberGiftNoteCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class SetMemberGiftNoteCommandHandler(IGroupRepository repository)
    : ICommandHandler<SetMemberGiftNoteCommand>
{
    /// <inheritdoc />
    public async Task HandleAsync(SetMemberGiftNoteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = await repository.RequireAsync(command.GroupId, cancellationToken);
        group.SetMemberGiftNote(command.MemberId, command.GiftNote);
        await repository.SaveAsync(group, cancellationToken);
    }
}
