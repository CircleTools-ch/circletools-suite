using CircleTools.Application;
using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Application;

/// <summary>
/// One person copied from a circle into a new group. This is a Secret-Santa-owned input shape: the
/// host translates circle DTOs into it so that <c>SecretSanta.Core</c> never references the Circles
/// context (ADR-0019). The domain is told an <em>account identifier</em>, never a user.
/// </summary>
/// <param name="CirclePersonId">
/// The circle person copied from, recorded on the member as its origin and never dereferenced.
/// </param>
/// <param name="Name">The person's display name.</param>
/// <param name="UserId">
/// The account backing this person, or <see langword="null"/> when the person has no account (a
/// managed member, or an unaccounted person). A person with an account becomes a <em>linked</em>
/// member; everyone else is brought in unlinked.
/// </param>
public sealed record CirclePersonInput(
    Guid CirclePersonId,
    string Name,
    Guid? UserId);

/// <summary>
/// Command to compose a brand-new group from a snapshot of circle people. The copy is taken at
/// create time and never tracks the circle afterwards (FR-027); the group is account-scoped from
/// birth and rides the normal sync/claim cycle with no separate claim step.
/// </summary>
/// <param name="Name">A non-empty group name.</param>
/// <param name="People">The chosen circle people, in display order.</param>
public sealed record CreateGroupFromCircleCommand(string Name, IReadOnlyList<CirclePersonInput> People)
    : ICommand<CreateGroupFromCircleResult>;

/// <summary>Outcome of <see cref="CreateGroupFromCircleCommand"/>.</summary>
/// <param name="GroupId">The identity of the created group.</param>
/// <param name="BroughtInUnlinkedNames">
/// The names of people who <em>had</em> an account but were brought in <strong>unlinked</strong>
/// because another chosen person already resolved to that same account — one user backs at most one
/// member per group. Surfaced so the UI can tell the user why (the duplicate-account notice). Empty
/// in the common case.
/// </param>
public sealed record CreateGroupFromCircleResult(Guid GroupId, IReadOnlyList<string> BroughtInUnlinkedNames);

/// <summary>Handles <see cref="CreateGroupFromCircleCommand"/>.</summary>
/// <param name="repository">The group repository port.</param>
public sealed class CreateGroupFromCircleCommandHandler(IGroupRepository repository)
    : ICommandHandler<CreateGroupFromCircleCommand, CreateGroupFromCircleResult>
{
    /// <inheritdoc />
    public async Task<CreateGroupFromCircleResult> HandleAsync(
        CreateGroupFromCircleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var group = Group.Create(command.Name);

        // One user backs at most one member per group. When two chosen people resolve to the same
        // account, the first keeps the link and the rest come in unlinked and are reported, so the
        // decision is made once here rather than surfacing later as a domain exception at accept.
        var linkedUsers = new HashSet<Guid>();
        var broughtInUnlinked = new List<string>();

        foreach (var person in command.People)
        {
            Guid? link = null;
            if (person.UserId is { } userId)
            {
                if (linkedUsers.Add(userId))
                {
                    link = userId;
                }
                else
                {
                    broughtInUnlinked.Add(person.Name);
                }
            }

            group.AddMemberFromCircle(person.Name, link, person.CirclePersonId);
        }

        await repository.SaveAsync(group, cancellationToken);
        return new CreateGroupFromCircleResult(group.Id, broughtInUnlinked);
    }
}
