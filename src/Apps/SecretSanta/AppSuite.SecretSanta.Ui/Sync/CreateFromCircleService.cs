using AppSuite.Application;
using AppSuite.ApiClient;
using AppSuite.Circles.Contracts;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Application;
using AppSuite.SecretSanta.LocalStore.Linking;

namespace AppSuite.SecretSanta.Ui;

/// <summary>
/// Reads the caller's circles for the create-from-circle picker and composes a new group from the
/// chosen people (FEAT-005, FR-027). The mapping from a circle person to a member lives here, in the
/// client — it translates a circle DTO into the Secret-Santa-owned <see cref="CirclePersonInput"/> so
/// the domain is told an account identifier and an opaque origin id, never a circle.
/// </summary>
/// <param name="circles">The read-only circle directory on the Main API.</param>
/// <param name="sender">The command dispatcher.</param>
/// <param name="linking">The linking surface client, to register the account-backed links server-side.</param>
public sealed class CreateFromCircleService(ICircleDirectoryApi circles, ISender sender, ILinkingApi linking)
{
    /// <summary>Lists the circles the caller belongs to, for the chooser.</summary>
    public Task<ApiResult<IReadOnlyList<CircleSummaryResponse>>> GetCirclesAsync(CancellationToken cancellationToken = default) =>
        circles.GetCirclesAsync(cancellationToken);

    /// <summary>Reads one circle's people, so the picker can pre-select them all.</summary>
    public Task<ApiResult<CircleDetailResponse>> GetCircleAsync(Guid circleId, CancellationToken cancellationToken = default) =>
        circles.GetCircleAsync(circleId, cancellationToken);

    /// <summary>Composes a new group from the chosen circle people, and registers the account-backed links server-side.</summary>
    /// <param name="circleId">The origin circle — the server checks shared membership against it.</param>
    /// <param name="groupName">A non-empty group name.</param>
    /// <param name="chosen">The chosen circle people, in display order.</param>
    /// <param name="cancellationToken">Cancels the command.</param>
    /// <returns>The created group's id and the names brought in unlinked because of a duplicate account.</returns>
    public async Task<CreateGroupFromCircleResult> CreateAsync(
        Guid circleId,
        string groupName,
        IEnumerable<CirclePersonResponse> chosen,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        var people = chosen.Select(ToInput).ToList();
        var result = await sender.Send(new CreateGroupFromCircleCommand(groupName, people), cancellationToken);

        // The command set Member.UserId locally; register each of those links server-side so the linked
        // person owns their wishlist and their reads are authorized. Re-read the group to learn the
        // member ids the command minted. The link is registered against the shared circle, which the
        // server verifies (FR-027, ADR-0018).
        var group = await sender.Send(new GetGroupQuery(result.GroupId), cancellationToken);
        if (group is not null)
        {
            foreach (var member in group.Members.Where(m => m.UserId is not null))
            {
                await linking.RegisterCircleLinkAsync(
                    new RegisterCircleLinkRequest(
                        result.GroupId, groupName, member.Id, member.Name, circleId, member.UserId!.Value),
                    cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// A circle person becomes a member input: the membership id is the recorded origin, an
    /// account-backed person carries their account id (so they come in linked), and a managed member —
    /// who has no account — comes in unlinked. Circles carry no wishlist, so it starts empty.
    /// </summary>
    public static CirclePersonInput ToInput(CirclePersonResponse person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return new CirclePersonInput(
            person.MembershipId,
            person.DisplayName,
            [],
            person.IsManaged ? null : person.AccountId);
    }
}
