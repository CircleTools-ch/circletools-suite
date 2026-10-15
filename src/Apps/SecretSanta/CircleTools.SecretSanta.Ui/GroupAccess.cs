using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// What this device may do with a group (ADR-0058): a device that joined through a join link is a
/// participant. It sees the group and its own assignment, but only the organizer changes the group, and
/// it never sees the other people's reveal links. Pages ask here instead of reading the sync record.
/// </summary>
/// <param name="local">The local store holding the group's sync record.</param>
public sealed class GroupAccess(ILocalSyncStore local)
{
    /// <summary>
    /// Reads a group's access through an optional <see cref="GroupAccess"/>: a host (or component test)
    /// without the sync engine has only organizer-owned groups.
    /// </summary>
    /// <param name="services">The page's service provider.</param>
    /// <param name="groupId">The group.</param>
    /// <returns>The access.</returns>
    public static Task<GroupAccessInfo> ForAsync(IServiceProvider services, Guid groupId)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService(typeof(GroupAccess)) is GroupAccess access
            ? access.GetAsync(groupId)
            : Task.FromResult(GroupAccessInfo.Organizer);
    }

    /// <summary>Reads whether this device holds the group only as a join-link participant.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The participant facts, or a regular (editable) access when the record can't be read.</returns>
    public async Task<GroupAccessInfo> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = (await local.LoadGroupsAsync(cancellationToken)).FirstOrDefault(r => r.State.Id == groupId);
            return record is null || !record.IsJoinOnly()
                ? GroupAccessInfo.Organizer
                : new GroupAccessInfo(ReadOnly: true, record.ClaimedMemberId);
        }
        catch (StorageUnavailableException)
        {
            return GroupAccessInfo.Organizer;
        }
    }
}

/// <summary>What this device may do with a group.</summary>
/// <param name="ReadOnly">Whether this device is a join-link participant that may only look.</param>
/// <param name="ClaimedMemberId">For a participant, the member it claimed ("this is me"), if any.</param>
public sealed record GroupAccessInfo(bool ReadOnly, Guid? ClaimedMemberId)
{
    /// <summary>The organizer's (or a plain local group's) full access.</summary>
    public static GroupAccessInfo Organizer { get; } = new(false, null);
}
