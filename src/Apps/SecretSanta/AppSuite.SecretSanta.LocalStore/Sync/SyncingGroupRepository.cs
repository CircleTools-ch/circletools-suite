using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Persistence;
using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore;

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

        // Only a claimed group needs a tombstone: the server holds a copy that must be told to delete,
        // and the account's other devices must drop theirs. An account-free group was never on the
        // server, so its deletion is purely local.
        if (removed.AccountScope is { } accountId)
        {
            var stateDoc = await local.LoadStateAsync(cancellationToken);
            var tombstone = new PendingTombstone(accountId, SyncKinds.Group, id, removed.Version);
            var tombstones = stateDoc.Tombstones
                .Where(t => t.Id != id)
                .Append(tombstone)
                .ToList();
            await local.SaveStateAsync(stateDoc with { Tombstones = tombstones }, cancellationToken);
        }
    }
}
