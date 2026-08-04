using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.LocalStore;

namespace AppSuite.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// One device in the two-client harness (FEAT-004): the real Secret-Santa client engine — a
/// <see cref="LocalSyncStore"/> over its own storage, the <see cref="SyncingGroupRepository"/> the
/// domain writes through, and the <see cref="SyncService"/> that runs a cycle — bound to a single
/// account's authorized transport. Nothing here is a mock of the client; the only test double is the
/// in-memory stand-in for the browser's storage.
/// </summary>
/// <remarks>
/// Editing goes through the same repository the app uses, so a local edit bumps the version and marks
/// the copy dirty exactly as it does in production; <see cref="SyncAsync"/> runs the same cycle every
/// trigger runs. <see cref="ClaimAsync"/> is the one deliberate stand-in: the claim <em>screen</em>
/// lands with a later slice, but claiming is only "scope this local group to the account and let the
/// normal cycle push it", which is what the method does — enough to exercise every convergence path
/// that a claimed group rides.
/// </remarks>
public sealed class SyncClient
{
    private readonly Guid _accountId;
    private readonly InMemoryKeyValueStore _storage = new();
    private readonly LocalSyncStore _local;
    private readonly SyncingGroupRepository _repository;
    private readonly SyncService _sync;

    /// <summary>Creates a device for <paramref name="accountId"/> over <paramref name="api"/>.</summary>
    /// <param name="accountId">The account this device syncs under.</param>
    /// <param name="api">The transport to the host (optionally decorated to model an interruption).</param>
    public SyncClient(Guid accountId, ISyncApi api)
    {
        _accountId = accountId;
        _local = new LocalSyncStore(_storage);
        _repository = new SyncingGroupRepository(_local);
        _sync = new SyncService(_local, api);
    }

    /// <summary>Whether the device's local storage refuses writes, as a full browser store would (ADR-0029).</summary>
    public bool StorageRefusesWrites
    {
        get => _storage.RefuseWrites;
        set => _storage.RefuseWrites = value;
    }

    /// <summary>Runs one sync cycle for the account — the single mechanism behind every trigger.</summary>
    /// <param name="cancellationToken">Cancels the cycle.</param>
    /// <returns>How the cycle ended and what moved.</returns>
    public Task<SyncOutcome> SyncAsync(CancellationToken cancellationToken = default) =>
        _sync.SyncNowAsync(_accountId, cancellationToken);

    /// <summary>Creates an account-free group with members (each with an empty wishlist), as a new group starts.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="memberNames">The members to add.</param>
    /// <returns>The new group's id.</returns>
    public async Task<Guid> CreateGroupAsync(string name, params string[] memberNames)
    {
        var group = Group.Create(name);
        foreach (var memberName in memberNames)
        {
            group.AddMember(memberName, Wishlist.Empty);
        }

        await _repository.SaveAsync(group, CancellationToken.None);
        return group.Id;
    }

    /// <summary>
    /// Creates a fully-populated group — members with wishlists, an exclusion, and a completed draw —
    /// so a convergence test can assert every part of a claimed group crosses to the other device, not
    /// just its name (FR-023: "members, wishlists, exclusions and a draw").
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <returns>The new group's id.</returns>
    public async Task<Guid> CreateGroupWithDrawAsync(string name)
    {
        var group = Group.Create(name);
        var ada = group.AddMember("Ada", Wishlist.Create(["Books", "Tea"]));
        var bern = group.AddMember("Bernd", Wishlist.Create(["Socks"]));
        var cora = group.AddMember("Cora", Wishlist.Empty);
        group.AddMember("Deniz", Wishlist.Create(["Chocolate"]));

        // One exclusion the draw must honour, so the "exclusions crossed too" assertion has teeth.
        group.AddExclusion(ada, bern);

        var result = group.RunDraw(new DrawGenerator(new SystemRandomSource()), DateTimeOffset.UtcNow);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("The seeded draw did not succeed; the harness group is misconfigured.");
        }

        _ = cora;
        await _repository.SaveAsync(group, CancellationToken.None);
        return group.Id;
    }

    /// <summary>
    /// Claims the given local group (or every account-free group when <paramref name="id"/> is null)
    /// into the account: scope it and mark it dirty, so the next cycle pushes it. The stand-in for the
    /// claim screen — see the type remarks.
    /// </summary>
    /// <param name="id">The group to claim, or null for all account-free groups.</param>
    public async Task ClaimAsync(Guid? id = null)
    {
        var records = (await _local.LoadGroupsAsync()).ToList();
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var applies = record.AccountScope is null && (id is null || record.State.Id == id);
            if (applies)
            {
                records[i] = record with { AccountScope = _accountId, Dirty = true };
            }
        }

        await _local.SaveGroupsAsync(records);
    }

    /// <summary>
    /// Removes a group from the account (FR-026): calls the real un-claim route and, once the server
    /// confirms, drops the local account scope while keeping the local copy. The counterpart to
    /// <see cref="ClaimAsync"/>, driven through the real <see cref="SyncService"/>.
    /// </summary>
    /// <param name="id">The group to remove from the account.</param>
    /// <returns>How the removal ended.</returns>
    public Task<SyncOutcome> UnclaimAsync(Guid id) => _sync.UnclaimAsync(_accountId, id);

    /// <summary>Renames a group, going through the domain so the edit bumps the version and marks it dirty.</summary>
    /// <param name="id">The group to rename.</param>
    /// <param name="name">The new name.</param>
    public async Task RenameGroupAsync(Guid id, string name)
    {
        var group = await _repository.GetAsync(id, CancellationToken.None)
            ?? throw new InvalidOperationException($"No local group {id} to rename.");
        group.Rename(name);
        await _repository.SaveAsync(group, CancellationToken.None);
    }

    /// <summary>Adds a member to a group through the domain (a real local edit).</summary>
    /// <param name="id">The group to edit.</param>
    /// <param name="memberName">The member to add.</param>
    public async Task AddMemberAsync(Guid id, string memberName)
    {
        var group = await _repository.GetAsync(id, CancellationToken.None)
            ?? throw new InvalidOperationException($"No local group {id} to edit.");
        group.AddMember(memberName, Wishlist.Empty);
        await _repository.SaveAsync(group, CancellationToken.None);
    }

    /// <summary>Deletes a group locally; a claimed group queues a tombstone for its next push.</summary>
    /// <param name="id">The group to delete.</param>
    public Task DeleteGroupAsync(Guid id) => _repository.DeleteAsync(id, CancellationToken.None);

    /// <summary>The groups the device currently holds, rebuilt from their mementos as the domain sees them.</summary>
    /// <returns>The device's groups.</returns>
    public async Task<IReadOnlyList<Group>> GroupsAsync() =>
        await _repository.ListAsync(CancellationToken.None);

    /// <summary>The device's copy of one group, or null if it holds none.</summary>
    /// <param name="id">The group id.</param>
    /// <returns>The group, or null.</returns>
    public Task<Group?> GroupAsync(Guid id) => _repository.GetAsync(id, CancellationToken.None);

    /// <summary>The device's raw records, carrying the sync metadata (version, scope, dirty).</summary>
    /// <returns>The stored records.</returns>
    public async Task<IReadOnlyList<SyncedGroupRecord>> RecordsAsync() => await _local.LoadGroupsAsync();

    /// <summary>The device's record for one group, or null.</summary>
    /// <param name="id">The group id.</param>
    /// <returns>The record, or null.</returns>
    public async Task<SyncedGroupRecord?> RecordAsync(Guid id) =>
        (await _local.LoadGroupsAsync()).FirstOrDefault(r => r.State.Id == id);

    /// <summary>How many groups the device holds locally — the count data-safety tests watch.</summary>
    /// <returns>The local group count.</returns>
    public async Task<int> LocalGroupCountAsync() => (await _local.LoadGroupsAsync()).Count;

    /// <summary>The cursor the device has reached on the account (its highest applied server sequence).</summary>
    /// <returns>The account cursor, or zero if it has never synced.</returns>
    public async Task<long> CursorAsync() => (await _local.LoadStateAsync()).CursorFor(_accountId);
}
