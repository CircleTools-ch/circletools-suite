using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The members of an online group who are <strong>connected</strong> to a person (ADR-0086): they take part (any
/// route — join link, invitation, circle, the organizer's own member) and see their own assignment in the app, or
/// were invited and haven't answered yet. They need no reveal link. Members who keep their own entry have read-only
/// gift notes and wishlists for the organizer.
/// </summary>
/// <param name="Active">Members a person takes part as.</param>
/// <param name="Invited">Members an invitation is waiting for.</param>
/// <param name="SelfMaintained">Members who keep their own wishlist and gift note.</param>
/// <param name="Mine">The member the signed-in organizer is, if any.</param>
public sealed record ConnectedMembers(
    IReadOnlySet<Guid> Active,
    IReadOnlySet<Guid> Invited,
    IReadOnlySet<Guid> SelfMaintained,
    Guid? Mine = null)
{
    /// <summary>Gets nobody connected.</summary>
    public static ConnectedMembers None { get; } = new(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());

    /// <summary>Whether the member keeps their own wishlist and gift note, so the organizer may not edit them.</summary>
    /// <param name="memberId">The member.</param>
    /// <returns><see langword="true"/> when self-maintained.</returns>
    public bool KeepsOwnEntry(Guid memberId) => SelfMaintained.Contains(memberId);

    /// <summary>Whether a person takes part as the member (sees the assignment in the app).</summary>
    /// <param name="memberId">The member.</param>
    /// <returns><see langword="true"/> when connected.</returns>
    public bool Contains(Guid memberId) => Active.Contains(memberId);

    /// <summary>Whether the member has a person, or an invitation waiting — no one else can be brought in as them.</summary>
    /// <param name="memberId">The member.</param>
    /// <returns><see langword="true"/> when taken.</returns>
    public bool IsTaken(Guid memberId) => Active.Contains(memberId) || Invited.Contains(memberId);

    /// <summary>Reads who is connected in a group from this device's record of it (as of the last sync).</summary>
    /// <param name="services">The page's service provider.</param>
    /// <param name="group">The group.</param>
    /// <returns>The connected members; nobody for a device group or a host without the record store.</returns>
    public static async Task<ConnectedMembers> ForAsync(IServiceProvider services, GroupDto group)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(group);

        if (services.GetService<IGroupRecordStore>() is not { } store)
        {
            return None;
        }

        GroupRecord? record;
        try
        {
            record = (await store.LoadAsync()).Records.FirstOrDefault(r => r.Id == group.Id);
        }
        catch (StorageUnavailableException)
        {
            return None;
        }

        if (record is not { Place: GroupPlace.Online })
        {
            return None;
        }

        var roles = record.Roles ?? [];
        var active = roles.Where(r => r is { MemberId: not null, Invited: false }).Select(r => r.MemberId!.Value).ToHashSet();
        var invited = roles.Where(r => r is { MemberId: not null, Invited: true }).Select(r => r.MemberId!.Value).ToHashSet();
        var mine = roles.FirstOrDefault(r => r.Role == "owner" && r.MemberId is not null)?.MemberId;
        return new ConnectedMembers(active, invited, (record.SelfMaintained ?? []).ToHashSet(), mine);
    }
}
