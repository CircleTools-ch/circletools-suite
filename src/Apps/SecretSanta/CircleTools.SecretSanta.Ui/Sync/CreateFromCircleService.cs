using CircleTools.Application;
using CircleTools.ApiClient;
using CircleTools.Circles.Contracts;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore.Linking;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Reads the caller's circles for the create-from-circle picker and composes a new group from the
/// chosen people (FEAT-005, FR-027). The mapping from a circle person to a member lives here, in the
/// client — it translates a circle DTO into the Secret-Santa-owned <see cref="CirclePersonInput"/> so
/// the domain is told an account identifier and an opaque origin id, never a circle.
/// </summary>
/// <param name="circles">The read-only circle directory on the Main API.</param>
/// <param name="sender">The command dispatcher.</param>
/// <param name="online">Puts the new group online and brings the circle's people in as participants (ADR-0086).</param>
/// <param name="auth">The signed-in organizer.</param>
public sealed class CreateFromCircleService(
    ICircleDirectoryApi circles,
    ISender sender,
    OnlineGroupSync online,
    AuthenticationStateProvider auth)
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

        // A group made from a circle is for the people in it, so it goes online at once (ADR-0086), owned by the
        // signed-in organizer. Everyone in the circle with an account takes part right away — verified against the
        // shared circle by the server (FR-027, ADR-0018); the organizer's own member is theirs. Best-effort: offline,
        // the group stays on this device and can be put online later.
        var me = (await auth.GetAuthenticationStateAsync()).User.UserId();
        if (me == Guid.Empty || !(await online.MakeOnlineAsync(result.GroupId, me, joinEnabled: false, cancellationToken)).IsSuccess)
        {
            return result;
        }

        foreach (var account in result.Accounts)
        {
            var request = account.AccountId == me
                ? new AddParticipantRequest(account.MemberId, ParticipantRoute.OwnMember)
                : new AddParticipantRequest(account.MemberId, ParticipantRoute.Circle, CircleId: circleId, AccountId: account.AccountId);
            await online.AddParticipantAsync(result.GroupId, request, cancellationToken);
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
            person.IsManaged ? null : person.AccountId);
    }
}
