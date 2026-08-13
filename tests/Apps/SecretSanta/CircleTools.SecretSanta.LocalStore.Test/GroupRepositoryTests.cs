using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="GroupRepository"/>, which serializes all groups as JSON under one key.
/// Verifies the real persistence round-trip (not just the in-memory domain fake).
/// </summary>
public sealed class GroupRepositoryTests
{
    private static Group CreateGroupWithMembers(string name, params string[] memberNames)
    {
        var group = Group.Create(name);
        foreach (var memberName in memberNames)
        {
            group.AddMember(memberName, Wishlist.Empty);
        }

        return group;
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsGroupState()
    {
        var repository = new GroupRepository(new InMemoryKeyValueStore());
        var group = CreateGroupWithMembers("Family", "Alice", "Bob");
        group.SetBudget(Budget.Create(25m, "CHF"));

        await repository.SaveAsync(group, default);

        var loaded = await repository.GetAsync(group.Id, default);
        Assert.NotNull(loaded);
        Assert.Equal("Family", loaded!.Name);
        Assert.Equal(2, loaded.Members.Count);
        Assert.Equal(25m, loaded.Budget!.Amount);
    }

    [Fact]
    public async Task Save_PersistsMultipleGroupsUnderOneKey()
    {
        var store = new InMemoryKeyValueStore();
        var repository = new GroupRepository(store);
        await repository.SaveAsync(CreateGroupWithMembers("One", "A", "B"), default);
        await repository.SaveAsync(CreateGroupWithMembers("Two", "C", "D"), default);

        var all = await repository.ListAsync(default);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, g => g.Name == "One");
        Assert.Contains(all, g => g.Name == "Two");
        // All groups live under a single storage key.
        Assert.NotNull(await store.GetAsync(StorageScope.Durable, GroupRepository.StorageKey, default));
    }

    [Fact]
    public async Task Save_ExistingGroup_UpdatesInPlace()
    {
        var repository = new GroupRepository(new InMemoryKeyValueStore());
        var group = CreateGroupWithMembers("Family", "Alice");
        await repository.SaveAsync(group, default);

        group.Rename("Renamed");
        await repository.SaveAsync(group, default);

        var all = await repository.ListAsync(default);
        var loaded = Assert.Single(all);
        Assert.Equal("Renamed", loaded.Name);
    }

    [Fact]
    public async Task Delete_RemovesOnlyTargetGroup()
    {
        var repository = new GroupRepository(new InMemoryKeyValueStore());
        var keep = CreateGroupWithMembers("Keep", "A");
        var drop = CreateGroupWithMembers("Drop", "B");
        await repository.SaveAsync(keep, default);
        await repository.SaveAsync(drop, default);

        await repository.DeleteAsync(drop.Id, default);

        var all = await repository.ListAsync(default);
        var loaded = Assert.Single(all);
        Assert.Equal(keep.Id, loaded.Id);
    }

    [Fact]
    public async Task Get_UnknownGroup_ReturnsNull()
    {
        var repository = new GroupRepository(new InMemoryKeyValueStore());

        Assert.Null(await repository.GetAsync(Guid.NewGuid(), default));
    }

    /// <summary>
    /// A refused write must not read as a successful save. This store holds the only copy of a
    /// group (NFR-001, no backend), so the shared <see cref="IKeyValueStore"/> contract's
    /// swallow-and-carry-on behaviour — right for a language preference — would be silent data
    /// loss here.
    /// </summary>
    [Fact]
    public async Task Save_WhenStorageRefusesTheWrite_Throws()
    {
        var store = new InMemoryKeyValueStore { RefuseWrites = true };
        var repository = new GroupRepository(store);

        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => repository.SaveAsync(CreateGroupWithMembers("Family", "Alice"), default));
    }

    [Fact]
    public async Task Delete_WhenStorageRefusesTheWrite_Throws()
    {
        var store = new InMemoryKeyValueStore();
        var repository = new GroupRepository(store);
        var group = CreateGroupWithMembers("Family", "Alice");
        await repository.SaveAsync(group, default);

        store.RefuseWrites = true;

        await Assert.ThrowsAsync<StorageUnavailableException>(
            () => repository.DeleteAsync(group.Id, default));
    }
}
