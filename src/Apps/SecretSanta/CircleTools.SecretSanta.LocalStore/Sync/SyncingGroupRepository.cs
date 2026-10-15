using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The syncing <see cref="IGroupRepository"/> for M3 (FEAT-004). It serves the domain port exactly as
/// <see cref="GroupRepository"/> did, and additionally keeps each group's sync metadata: a local edit
/// is written first, bumps the version and is flagged dirty, with <em>no awaited network call</em> —
/// the write takes full effect offline and the <see cref="SyncService"/> reconciles later (NFR-008).
/// </summary>
/// <remarks>
/// The domain is unaware of any of this. It hands over a <see cref="Group"/>; the version, the account
/// scope and the dirty flag are the adapter's, wrapped around the pristine memento in a
/// <see cref="SyncedGroupRecord"/>. Deleting a group that has been claimed queues a tombstone so the
/// deletion reaches the account's other devices; deleting an account-free group simply drops it, since
/// the server never held a copy.
/// </remarks>
/// <param name="local">The shared persistence gateway.</param>
public sealed class SyncingGroupRepository(ILocalSyncStore local) : IGroupRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        var records = await local.LoadGroupsAsync(cancellationToken);
        return records.Select(r => Group.FromState(r.State)).ToList();
    }

    /// <inheritdoc />
    public async Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var records = await local.LoadGroupsAsync(cancellationToken);
        var record = records.FirstOrDefault(r => r.State.Id == id);
        return record is null ? null : Group.FromState(record.State);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var state = group.ToState();
        var index = records.FindIndex(r => r.State.Id == group.Id);

        if (index >= 0)
        {
            var existing = records[index];

            // A join-only participant has no way to send a group edit (ADR-0058), so an edit here would only
            // ever exist on this device. The UI offers none; this keeps a missed path from diverging silently.
            if (existing.IsJoinOnly())
            {
                throw new GroupReadOnlyException(group.Id);
            }

            // Every local edit bumps the version and marks the copy dirty. The account scope is
            // preserved across the edit — editing a claimed group keeps it claimed.
            records[index] = existing with
            {
                State = state,
                Version = existing.Version + 1,
                Dirty = true,
            };
        }
        else
        {
            // A brand-new group is account-free until it is claimed; it carries version 1.
            records.Add(new SyncedGroupRecord(state, Version: 1, AccountScope: null, Dirty: true));
        }

        await local.SaveGroupsAsync(records, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == id);
        if (index < 0)
        {
            return;
        }

        var removed = records[index];
        records.RemoveAt(index);
        await local.SaveGroupsAsync(records, cancellationToken);

        // A join-only participant removing the group only leaves it on this device. The group, its links and
        // its linking projection are the organizer's; nothing about them may be retracted or deleted from here.
        if (removed.IsJoinOnly())
        {
            return;
        }

        // Only a claimed group needs a tombstone: the server holds a copy that must be told to delete,
        // and the account's other devices must drop theirs. An account-free group was never on the
        // server, so its deletion is purely local.
        var claimed = removed.AccountScope is not null;

        // Any group with a linked member has had its linking projection published (event registry,
        // assignments, member-links) — whatever its storage mode. Queue a retraction so the next signed-in
        // sync removes it and the group leaves every linked member's My assignments / My wishlists
        // (ADR-0077). Queued rather than called here: deleting works offline, the retraction follows.
        var linked = removed.State.Members.Any(m => m.UserId is not null);

        // A "My account" group may also have an online copy in the shared-group store — an admin-claimed
        // shared group, or one an older auto-claim put in both stores — that only its owner can delete.
        // Queue that delete, or the next owned-group discovery downloads the group again (ADR-0078). Not
        // for a group still waiting to be claimed: nothing of it is online yet.
        var ownedOnline = (removed.Mode == StorageMode.Account || removed.AccountScope is not null)
            && removed.PendingClaimAccount is null;

        if (claimed || linked || ownedOnline)
        {
            var stateDoc = await local.LoadStateAsync(cancellationToken);
            if (removed.AccountScope is { } accountId)
            {
                var tombstone = new PendingTombstone(accountId, SyncKinds.Group, id, removed.Version);
                stateDoc = stateDoc with
                {
                    Tombstones = stateDoc.Tombstones.Where(t => t.Id != id).Append(tombstone).ToList(),
                };
            }

            if (linked)
            {
                stateDoc = stateDoc with
                {
                    Retractions = (stateDoc.Retractions ?? []).Where(r => r != id).Append(id).ToList(),
                };
            }

            if (ownedOnline)
            {
                var ownedDelete = new PendingOwnedDelete(id, removed.AdminAccount ?? removed.AccountScope);
                stateDoc = stateDoc with
                {
                    OwnedDeletes = (stateDoc.OwnedDeletes ?? []).Where(d => d.GroupId != id).Append(ownedDelete).ToList(),
                };
            }

            await local.SaveStateAsync(stateDoc, cancellationToken);
        }
    }
}
