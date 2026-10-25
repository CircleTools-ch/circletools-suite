using System.Security.Claims;
using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The visibility rule of <see cref="VisibleGroupsService"/> (ADR-0044, ADR-0086): groups on this device and groups
/// held by a link or device key always show; an account's online groups show only while that account is signed in
/// (kept on the device, never shown to another account). Groups taken part in are listed apart.
/// </summary>
public sealed class VisibleGroupsServiceTests
{
    private static readonly Guid DeviceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MineId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OthersId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LinkId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TakingPartId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Account = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OtherAccount = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task SignedOut_ShowsDeviceAndLinkGroups_NotAccountGroups()
    {
        var visible = await (await BuildAsync(Guid.Empty)).GetVisibleAsync();

        Assert.Equal([DeviceId, LinkId], visible.Select(g => g.Id).Order());
    }

    [Fact]
    public async Task SignedIn_AddsThatAccountsGroups_NeverAnotherAccounts()
    {
        var visible = await (await BuildAsync(Account)).GetVisibleAsync();

        Assert.Equal([DeviceId, MineId, LinkId], visible.Select(g => g.Id).Order());
    }

    [Fact]
    public async Task GroupsTakenPart_AreListedApart_ForTheirAccountOnly()
    {
        Assert.Equal("Wichteln", Assert.Single(await (await BuildAsync(Account)).GetTakingPartAsync()).GroupName);
        Assert.Empty(await (await BuildAsync(OtherAccount)).GetTakingPartAsync());
    }

    private static async Task<VisibleGroupsService> BuildAsync(Guid signedInAs)
    {
        IReadOnlyList<GroupSummaryDto> summaries =
        [
            new(DeviceId, "Device", 0, 0, false, false),
            new(MineId, "Mine", 0, 0, false, false),
            new(OthersId, "Others", 0, 0, false, false),
            new(LinkId, "Link", 0, 0, false, false),
        ];

        var store = new GroupRecordStore(new MemoryKeyValueStore());
        await store.SaveAsync(GroupRecordsDocument.Empty with
        {
            Records =
            [
                new GroupRecord(DeviceId, GroupPlace.Device, State(DeviceId), null, 0, false),
                Online(MineId, new OnlineAccess(AccessPrincipal.Account, "owner", [], AccountId: Account)),
                Online(OthersId, new OnlineAccess(AccessPrincipal.Account, "owner", [], AccountId: OtherAccount)),
                Online(LinkId, new OnlineAccess(AccessPrincipal.OrganizerLink, "owner", [], Secret: "token")),
                new GroupRecord(TakingPartId, GroupPlace.Online, null,
                    new ParticipantView(TakingPartId, "Wichteln", 1, Guid.NewGuid(), "Ben", null, [], []), 1, false,
                    new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: Account)),
            ],
        });

        return new VisibleGroupsService(new StubSender(summaries), store, new StubAuth(signedInAs));
    }

    private static GroupRecord Online(Guid id, OnlineAccess access) =>
        new(id, GroupPlace.Online, State(id), null, 1, false, access);

    private static GroupState State(Guid id) => new(id, "G", null, null, [], [], []);

    private sealed class StubSender(IReadOnlyList<GroupSummaryDto> groups) : ISender
    {
        public Task Send(ICommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResult)(object)groups);
    }

    private sealed class StubAuth(Guid accountId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = accountId == Guid.Empty
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
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
