using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Deleting the account settles this device's groups (FR-043, ADR-0086): a group it organized stays as a device
/// group, a group it took part in leaves the device, and another account's groups are untouched. Before, they all
/// stayed hidden on the device for good (2026-10-25 test).
/// </summary>
public sealed class AccountDeletedTests
{
    private static readonly Guid Deleted = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    [Fact]
    public async Task OrganizedGroups_StayAsDeviceGroups_TakingPart_LeavesTheDevice()
    {
        var organized = Guid.NewGuid();
        var takingPart = Guid.NewGuid();
        var othersGroup = Guid.NewGuid();
        var keyValues = new MemoryKeyValueStore();
        var store = OnlineRecords.Seed(
            keyValues,
            OnlineRecords.Organized(organized) with { Access = OnlineRecords.Organized(organized).Access! with { AccountId = Deleted } },
            new GroupRecord(takingPart, GroupPlace.Online, null,
                new ParticipantView(takingPart, "Wichteln", 1, Guid.NewGuid(), "Kim", null, [], []), 1, false,
                new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: Deleted)),
            OnlineRecords.Organized(othersGroup) with { Access = OnlineRecords.Organized(othersGroup).Access! with { AccountId = Other } });

        await new GroupsOnAccountDeleted(new OnlineGroupSync(store, new FakeOnlineGroupApi())).OnAccountDeletedAsync(Deleted);

        var records = (await store.LoadAsync()).Records;
        var kept = Assert.Single(records, r => r.Id == organized);
        Assert.Equal(GroupPlace.Device, kept.Place);
        Assert.Null(kept.Access);
        Assert.NotNull(kept.Document);
        Assert.DoesNotContain(records, r => r.Id == takingPart);
        Assert.Equal(GroupPlace.Online, Assert.Single(records, r => r.Id == othersGroup).Place);
    }

    private sealed class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _values = [];

        public ValueTask<string?> GetAsync(StorageScope scope, string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public ValueTask<bool> SetAsync(StorageScope scope, string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return ValueTask.FromResult(true);
        }

        public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }
}
