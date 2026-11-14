// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// A queued server delete leaves the queue once the server took it (ADR-0086). It stayed queued for good: the sent
/// removal was looked up by record equality in a freshly read copy of the queue, whose permission list is another
/// instance, so nothing matched and every later cycle sent the delete again — a 404 in the console each time
/// (SS-5, 2026-10-28).
/// </summary>
public sealed class PendingRemovalTests
{
    [Fact]
    public async Task ADeleteTheServerTook_LeavesTheQueue_AndIsNotSentAgain()
    {
        var groupId = Guid.NewGuid();
        var store = OnlineRecords.Seed(new InMemoryStore());
        var access = OnlineRecords.Organized(groupId).Access!;
        await store.SaveAsync((await store.LoadAsync()) with { Removals = [new PendingRemoval(groupId, access, Leave: false)] });
        var api = new FakeOnlineGroupApi { DeleteSucceeds = true };
        var sync = new OnlineGroupSync(store, api);

        await sync.SyncAsync(access.AccountId);
        await sync.SyncAsync(access.AccountId);

        Assert.Empty((await store.LoadAsync()).Removals);
        Assert.Equal([groupId], api.Deleted);
    }

    [Fact]
    public async Task ADeleteThatCouldNotBeSent_StaysQueued()
    {
        var groupId = Guid.NewGuid();
        var store = OnlineRecords.Seed(new InMemoryStore());
        var access = OnlineRecords.Organized(groupId).Access!;
        await store.SaveAsync((await store.LoadAsync()) with { Removals = [new PendingRemoval(groupId, access, Leave: false)] });

        await new OnlineGroupSync(store, new FakeOnlineGroupApi()).SyncAsync(access.AccountId);

        Assert.Single((await store.LoadAsync()).Removals);
    }

    private sealed class InMemoryStore : IKeyValueStore
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
