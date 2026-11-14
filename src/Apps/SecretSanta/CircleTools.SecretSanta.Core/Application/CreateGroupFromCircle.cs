// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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
/// The account backing this person, or <see langword="null"/> when the person has no account (a managed
/// member, or an unaccounted person). It is not written into the group: the result pairs it with the new
/// member, so the caller can make that account a participant (ADR-0086).
/// </param>
public sealed record CirclePersonInput(
    Guid CirclePersonId,
    string Name,
    Guid? UserId);

/// <summary>
/// Command to compose a brand-new group from a snapshot of circle people. The copy is taken at create time
/// and never tracks the circle afterwards (FR-027).
/// </summary>
/// <param name="Name">A non-empty group name.</param>
/// <param name="People">The chosen circle people, in display order.</param>
public sealed record CreateGroupFromCircleCommand(string Name, IReadOnlyList<CirclePersonInput> People)
    : ICommand<CreateGroupFromCircleResult>;

/// <summary>A new member whose circle person has an account (ADR-0086): the caller makes that account a participant.</summary>
/// <param name="MemberId">The new member.</param>
/// <param name="AccountId">The account behind the circle person.</param>
public sealed record CircleMemberAccount(Guid MemberId, Guid AccountId);

/// <summary>Outcome of <see cref="CreateGroupFromCircleCommand"/>.</summary>
/// <param name="GroupId">The identity of the created group.</param>
/// <param name="BroughtInUnlinkedNames">
/// The names of people who <em>had</em> an account but come in without it, because another chosen person
/// already resolved to that same account — one account is at most one member per group. Surfaced so the UI
/// can tell the user why (the duplicate-account notice). Empty in the common case.
/// </param>
/// <param name="Accounts">The members whose account takes part, one per account.</param>
public sealed record CreateGroupFromCircleResult(
    Guid GroupId,
    IReadOnlyList<string> BroughtInUnlinkedNames,
    IReadOnlyList<CircleMemberAccount> Accounts);

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

        // One account is at most one member per group. When two chosen people resolve to the same account,
        // the first keeps it and the rest come in without it and are reported, so the decision is made once
        // here rather than surfacing later as a refused participant.
        var accounts = new List<CircleMemberAccount>();
        var broughtInUnlinked = new List<string>();

        foreach (var person in command.People)
        {
            var memberId = group.AddMemberFromCircle(person.Name, person.CirclePersonId);
            if (person.UserId is not { } userId)
            {
                continue;
            }

            if (accounts.Any(a => a.AccountId == userId))
            {
                broughtInUnlinked.Add(person.Name);
            }
            else
            {
                accounts.Add(new CircleMemberAccount(memberId, userId));
            }
        }

        await repository.SaveAsync(group, cancellationToken);
        return new CreateGroupFromCircleResult(group.Id, broughtInUnlinked, accounts);
    }
}
