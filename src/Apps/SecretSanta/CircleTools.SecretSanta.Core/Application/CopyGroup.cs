using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// Command to start a new group from an existing one — "Use as template for a new group" (Q34): the members
/// (with their gift notes) and the exclusions are copied; events, wishlists, draws and account links are not.
/// </summary>
/// <param name="SourceGroupId">The group to copy.</param>
/// <param name="Name">The new group's name.</param>
public sealed record CopyGroupCommand(Guid SourceGroupId, string Name) : ICommand<Guid>;

/// <summary>Handles <see cref="CopyGroupCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class CopyGroupCommandHandler(IGroupRepository repository)
    : ICommandHandler<CopyGroupCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CopyGroupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await repository.RequireAsync(command.SourceGroupId, cancellationToken);
        var copy = Group.Create(command.Name);
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var member in source.Members)
        {
            var id = copy.AddMember(member.Name);
            if (!string.IsNullOrEmpty(member.GiftNote))
            {
                copy.SetMemberGiftNote(id, member.GiftNote);
            }

            idMap[member.Id] = id;
        }

        foreach (var exclusion in source.Exclusions)
        {
            if (idMap.TryGetValue(exclusion.GiverMemberId, out var giver) && idMap.TryGetValue(exclusion.RecipientMemberId, out var recipient))
            {
                copy.AddExclusion(giver, recipient, exclusion.Mutual);
            }
        }

        await repository.SaveAsync(copy, cancellationToken);
        return copy.Id;
    }
}
