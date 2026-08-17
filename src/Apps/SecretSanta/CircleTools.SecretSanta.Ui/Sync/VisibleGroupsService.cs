using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The account-scoped group list every group-listing surface reads (ADR-0044): <strong>all</strong>
/// groups while signed in, <strong>only account-free</strong> groups while signed out.
/// </summary>
/// <remarks>
/// <para>
/// Signing out hides account groups from the offline view without deleting them — they stay cached and
/// reappear on the next sign-in (ADR-0044). Because the account scope lives only in the LocalStore's
/// sync metadata and deliberately not on the domain <see cref="GroupSummaryDto"/> (ADR-0016), the
/// filter is applied here in the read/UI layer over <see cref="ClaimableGroupsService"/>, leaving the
/// domain query untouched.
/// </para>
/// <para>
/// If the sync metadata cannot be read while signed out, the domain list is returned unfiltered — the
/// same degradation the existing screens use, and in practice unreachable, since the group query reads
/// the very same local store.
/// </para>
/// </remarks>
/// <param name="sender">The CQRS sender, for the domain group summaries.</param>
/// <param name="claimable">The sync-metadata read model, for each group's account scope.</param>
/// <param name="auth">The current principal, for whether a session is live.</param>
public sealed class VisibleGroupsService(
    ISender sender,
    ClaimableGroupsService claimable,
    AuthenticationStateProvider auth)
{
    /// <summary>Reads the groups that should be visible for the current sign-in state.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>All groups when signed in; only account-free groups when signed out.</returns>
    public async Task<IReadOnlyList<GroupSummaryDto>> GetVisibleAsync(CancellationToken cancellationToken = default)
    {
        var groups = await sender.Send(new GetGroupsQuery(), cancellationToken);

        var state = await auth.GetAuthenticationStateAsync();
        if (state.User.UserId() != Guid.Empty)
        {
            return groups;
        }

        try
        {
            var all = await claimable.GetAllAsync(cancellationToken);
            var accountFree = all.Where(g => g.IsAccountFree).Select(g => g.Id).ToHashSet();
            return [.. groups.Where(g => accountFree.Contains(g.Id))];
        }
        catch (StorageUnavailableException)
        {
            return groups;
        }
    }
}
