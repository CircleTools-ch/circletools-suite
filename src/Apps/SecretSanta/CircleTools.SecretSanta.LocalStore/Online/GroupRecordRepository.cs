// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>
/// The domain's <see cref="IGroupRepository"/> over this device's group records (ADR-0086). It sees the groups whose
/// whole document this device holds — device groups and online groups it organizes; a group it only takes part in
/// has no document and is read through the participant view instead.
/// </summary>
/// <param name="store">The record store.</param>
public sealed class GroupRecordRepository(IGroupRecordStore store) : IGroupRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        return [.. document.Records.Where(r => r.Document is not null).Select(r => Group.FromState(r.Document!))];
    }

    /// <inheritdoc />
    public async Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var record = document.Records.FirstOrDefault(r => r.Id == id);
        return record?.Document is { } state ? Group.FromState(state) : null;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        var document = await store.LoadAsync(cancellationToken);
        var records = document.Records.ToList();
        var index = records.FindIndex(r => r.Id == group.Id);
        if (index < 0)
        {
            // A new group starts on this device; making it online is a separate, explicit step.
            records.Add(new GroupRecord(group.Id, GroupPlace.Device, group.ToState(), null, 0, Dirty: false));
        }
        else
        {
            var existing = records[index];

            // Only a holder of the write-document permission can send an edit; anyone else's edit would exist on
            // this device only. The UI offers none; this keeps a missed path from diverging silently.
            if (!existing.CanEditDocument)
            {
                throw new GroupReadOnlyException(group.Id);
            }

            records[index] = existing with { Document = group.ToState(), Dirty = existing.Place == GroupPlace.Online };
        }

        await store.SaveAsync(document with { Records = records }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await store.LoadAsync(cancellationToken);
        var record = document.Records.FirstOrDefault(r => r.Id == id);
        if (record is null)
        {
            return;
        }

        var removals = document.Removals.Where(r => r.GroupId != id).ToList();

        // Deleting an online group this device may delete removes it for everyone, and works offline: the server
        // delete is queued for the next sync. Without that permission it is only removed from this device.
        if (record is { Place: GroupPlace.Online, Access: { } access } && access.Can(OnlinePermissions.DeleteGroup))
        {
            removals.Add(new PendingRemoval(id, access, Leave: false));
        }

        await store.SaveAsync(
            document with { Records = [.. document.Records.Where(r => r.Id != id)], Removals = removals },
            cancellationToken);
    }
}
