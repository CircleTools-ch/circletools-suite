// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="BackupService"/> — the portable export/import of the locally held groups
/// (FEAT-017). Exercised over the real <see cref="GroupRecordRepository"/> so the whole serialize → file →
/// deserialize → rehydrate round-trip is covered, not just an in-memory fake.
/// </summary>
public sealed class BackupServiceTests
{
    private static GroupRecordRepository NewRepository() => new(new GroupRecordStore(new InMemoryKeyValueStore()));

    private static Group CreateGroup(string name, params string[] memberNames)
    {
        var group = Group.Create(name);
        foreach (var memberName in memberNames)
        {
            group.AddMember(memberName);
        }

        return group;
    }

    [Fact]
    public async Task ExportThenImport_RoundTripsGroupsIntoAnEmptyDevice()
    {
        var source = NewRepository();
        var group = CreateGroup("Family", "Alice", "Bob");
        var eventId = group.AddEvent("Christmas");
        group.SetEventBudget(eventId, Budget.Create(25m, "CHF"));
        await source.SaveAsync(group, default);

        var json = await new BackupService(source).ExportAsync(default);

        // Restore into a fresh device with nothing on it.
        var target = NewRepository();
        var result = await new BackupService(target).ImportAsync(json, default);

        Assert.Equal(BackupImportOutcome.Restored, result.Outcome);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);

        var restored = Assert.Single(await target.ListAsync(default));
        Assert.Equal(group.Id, restored.Id);
        Assert.Equal("Family", restored.Name);
        Assert.Equal(2, restored.Members.Count);
        Assert.Equal(25m, restored.FindEvent(eventId)!.Budget!.Amount);
    }

    [Fact]
    public async Task Export_EmptyDevice_ProducesAValidEmptyBackup()
    {
        var json = await new BackupService(NewRepository()).ExportAsync(default);

        var document = JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupDocument);
        Assert.NotNull(document);
        Assert.Equal(BackupService.Format, document!.Format);
        Assert.Equal(BackupService.CurrentVersion, document.Version);
        Assert.Empty(document.Groups);
    }

    [Fact]
    public async Task Import_MergesById_AddingNewOverwritingSameIdAndDeletingNothing()
    {
        // The backup was taken with two groups: an updated copy of one already on the device, plus a new one.
        var backupSource = NewRepository();
        var existing = CreateGroup("Family", "Alice");
        existing.Rename("Family (renamed in backup)");
        var brandNew = CreateGroup("Friends", "Carol");
        await backupSource.SaveAsync(existing, default);
        await backupSource.SaveAsync(brandNew, default);
        var json = await new BackupService(backupSource).ExportAsync(default);

        // The device already holds the pre-backup "Family" (same id) and an unrelated group not in the backup.
        var device = NewRepository();
        var untouched = CreateGroup("Colleagues", "Dave");
        await device.SaveAsync(Group.FromState(WithName(existing, "Family")), default);
        await device.SaveAsync(untouched, default);

        var result = await new BackupService(device).ImportAsync(json, default);

        Assert.Equal(BackupImportOutcome.Restored, result.Outcome);
        Assert.Equal(1, result.Added);   // Friends
        Assert.Equal(1, result.Updated); // Family overwritten
        Assert.Equal(0, result.Skipped);

        var all = await device.ListAsync(default);
        Assert.Equal(3, all.Count); // nothing deleted: Family + Friends + Colleagues
        Assert.Equal("Family (renamed in backup)", all.Single(g => g.Id == existing.Id).Name);
        Assert.Contains(all, g => g.Id == brandNew.Id);
        Assert.Contains(all, g => g.Id == untouched.Id); // the group not in the backup survives
    }

    [Fact]
    public async Task Import_NonBackupFile_IsRejectedAndChangesNothing()
    {
        var device = NewRepository();
        await device.SaveAsync(CreateGroup("Family", "Alice"), default);

        var result = await new BackupService(device).ImportAsync("{\"hello\":\"world\"}", default);

        Assert.Equal(BackupImportOutcome.Invalid, result.Outcome);
        Assert.Single(await device.ListAsync(default)); // untouched
    }

    [Fact]
    public async Task Import_MalformedJson_IsRejected()
    {
        var result = await new BackupService(NewRepository()).ImportAsync("not json at all", default);

        Assert.Equal(BackupImportOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Import_NewerBackupVersion_IsRefusedWithoutWritingAnything()
    {
        var group = CreateGroup("Family", "Alice");
        var fromFuture = new BackupDocument(
            BackupService.Format,
            BackupService.CurrentVersion + 1,
            DateTimeOffset.UtcNow,
            [group.ToState()]);
        var json = JsonSerializer.Serialize(fromFuture, BackupJsonContext.Default.BackupDocument);

        var device = NewRepository();
        var result = await new BackupService(device).ImportAsync(json, default);

        Assert.Equal(BackupImportOutcome.UnsupportedVersion, result.Outcome);
        Assert.Empty(await device.ListAsync(default));
    }

    [Fact]
    public async Task Import_SkipsAMalformedEntryButRestoresTheRest()
    {
        // Start from a valid one-group backup, then splice a null entry into the groups array — the kind of
        // single corrupt record that must be skipped rather than failing the whole restore.
        var source = NewRepository();
        var good = CreateGroup("Family", "Alice");
        await source.SaveAsync(good, default);
        var json = (await new BackupService(source).ExportAsync(default))
            .Replace("\"Groups\": [", "\"Groups\": [\n    null,", StringComparison.Ordinal);

        var device = NewRepository();
        var result = await new BackupService(device).ImportAsync(json, default);

        Assert.Equal(BackupImportOutcome.Restored, result.Outcome);
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(good.Id, Assert.Single(await device.ListAsync(default)).Id);
    }

    // Rehydrate a group with a different name, keeping its id — used to seed the pre-backup version on
    // the device so the import exercises the same-id overwrite path.
    private static GroupState WithName(Group group, string name) => group.ToState() with { Name = name };
}
