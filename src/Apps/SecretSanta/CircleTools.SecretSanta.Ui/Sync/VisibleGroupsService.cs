using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The groups every group-listing surface shows (ADR-0086): groups on this device, groups held through a link or a
/// device key, and — while signed in — the signed-in account's online groups. A group that belongs to a
/// <em>different</em> account still cached on this device is never shown. A session waiting for the server (an
/// offline cold start) counts as signed in to its account: offline is not signed out.
/// </summary>
/// <param name="sender">The application dispatcher, for the group summaries.</param>
/// <param name="store">This device's group records.</param>
/// <param name="session">The account whose data this device shows.</param>
public sealed class VisibleGroupsService(ISender sender, IGroupRecordStore store, SessionAccount session)
{
    /// <summary>The groups this device organizes (or holds on this device only) that are visible now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The summaries.</returns>
    public async Task<IReadOnlyList<GroupSummaryDto>> GetVisibleAsync(CancellationToken cancellationToken = default)
    {
        var groups = await sender.Send(new GetGroupsQuery(), cancellationToken);
        var visible = (await VisibleRecordsAsync(cancellationToken)).Select(r => r.Id).ToHashSet();
        return [.. groups.Where(g => visible.Contains(g.Id))];
    }

    /// <summary>The groups the signed-in person (or this device) takes part in without organizing them.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The participant views held on this device, by group name.</returns>
    public async Task<IReadOnlyList<ParticipantView>> GetTakingPartAsync(CancellationToken cancellationToken = default) =>
        [.. (await VisibleRecordsAsync(cancellationToken))
            .Where(r => r.Document is null && r.View is not null)
            .Select(r => r.View!)
            .OrderBy(v => v.GroupName, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The invitations waiting for the signed-in account's answer, as last synced.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The invitations; none while signed out.</returns>
    public async Task<IReadOnlyList<MyGroup>> GetInvitationsAsync(CancellationToken cancellationToken = default)
    {
        if (await SignedInAsync() is null)
        {
            return [];
        }

        try
        {
            return (await store.LoadAsync(cancellationToken)).Invitations;
        }
        catch (StorageUnavailableException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<GroupRecord>> VisibleRecordsAsync(CancellationToken cancellationToken)
    {
        var account = await SignedInAsync();
        try
        {
            return [.. (await store.LoadAsync(cancellationToken)).Records.Where(r => r.IsVisibleTo(account))];
        }
        catch (StorageUnavailableException)
        {
            return [];
        }
    }

    private async Task<Guid?> SignedInAsync() => await session.CurrentAsync();
}
