using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.LocalStore;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The members of a group who are <strong>connected</strong> — they see their own assignment in the app,
/// either because their member is linked to an account or because they joined through the join link
/// (Q20, Q30, ADR-0080). They need no reveal link from the organizer, and while they are joined they keep
/// their own wishlist and gift note.
/// </summary>
/// <param name="Linked">Members linked to an account.</param>
/// <param name="Joined">Members someone claimed through the join link.</param>
/// <param name="SelfMaintained">
/// Members who keep their own wishlist and gift note on the server — joined, or edited from their reveal link
/// (ADR-0084) — so the organizer's copy of those fields is read-only.
/// </param>
public sealed record ConnectedMembers(IReadOnlySet<Guid> Linked, IReadOnlySet<Guid> Joined, IReadOnlySet<Guid> SelfMaintained)
{
    /// <summary>Gets nobody connected.</summary>
    public static ConnectedMembers None { get; } = new(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid>());

    /// <summary>Whether the member keeps their own wishlist and gift note, so the organizer may not edit them.</summary>
    /// <param name="memberId">The member.</param>
    /// <returns><see langword="true"/> when joined or self-maintained.</returns>
    public bool KeepsOwnEntry(Guid memberId) => Joined.Contains(memberId) || SelfMaintained.Contains(memberId);

    /// <summary>Whether the member sees their assignment in the app.</summary>
    /// <param name="memberId">The member.</param>
    /// <returns><see langword="true"/> when linked or joined.</returns>
    public bool Contains(Guid memberId) => Linked.Contains(memberId) || Joined.Contains(memberId);

    /// <summary>
    /// Reads who is connected in a group: linked members from the group, joined members from the server for a
    /// shared group this device organizes. A host without the sync engine (a component test) reads linked only.
    /// </summary>
    /// <param name="services">The page's service provider.</param>
    /// <param name="group">The group.</param>
    /// <returns>The connected members; joined ones are empty when the server can't be reached.</returns>
    public static async Task<ConnectedMembers> ForAsync(IServiceProvider services, GroupDto group)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(group);

        var linked = group.Members.Where(m => m.UserId is not null).Select(m => m.Id).ToHashSet();
        if (services.GetService<ILocalSyncStore>() is not { } local || services.GetService<SharedSyncService>() is not { } shared)
        {
            return new ConnectedMembers(linked, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var record = (await local.LoadGroupsAsync()).FirstOrDefault(r => r.State.Id == group.Id);
        var organizesShared = record is not null
            && !record.IsJoinOnly()
            && (record is { Mode: StorageMode.SharedByLink } || record.IsAdminClaimedShared());
        if (!organizesShared)
        {
            return new ConnectedMembers(linked, new HashSet<Guid>(), new HashSet<Guid>());
        }

        var claims = await shared.GetMemberClaimsAsync(group.Id);
        return new ConnectedMembers(linked, claims.MemberIds.ToHashSet(), (claims.SelfMaintained ?? []).ToHashSet());
    }
}
