using System.Security.Claims;
using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The account-scoped visibility rule of <see cref="VisibleGroupsService"/> (ADR-0044): signed in shows
/// every group, signed out shows only the account-free ones — account groups are hidden from the offline
/// view (kept on the device, not deleted).
/// </summary>
public sealed class VisibleGroupsServiceTests
{
    private static readonly Guid AccountFreeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountScopedId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Account = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SignedOut_HidesAccountScopedGroups()
    {
        var service = Build(signedInAs: Guid.Empty);

        var visible = await service.GetVisibleAsync();

        Assert.Equal([AccountFreeId], visible.Select(g => g.Id));
    }

    [Fact]
    public async Task SignedIn_ShowsEveryGroup()
    {
        var service = Build(signedInAs: Account);

        var visible = await service.GetVisibleAsync();

        Assert.Equal([AccountFreeId, AccountScopedId], visible.Select(g => g.Id));
    }

    private static VisibleGroupsService Build(Guid signedInAs)
    {
        IReadOnlyList<GroupSummaryDto> summaries =
        [
            new(AccountFreeId, "Offline", 0, 0, false, false),
            new(AccountScopedId, "Synced", 0, 0, false, false),
        ];

        IReadOnlyList<SyncedGroupRecord> records =
        [
            new(State(AccountFreeId), Version: 1, AccountScope: null, Dirty: false),
            new(State(AccountScopedId), Version: 1, AccountScope: Account, Dirty: false),
        ];

        var claimable = new ClaimableGroupsService(new StubLocalSyncStore(records));
        return new VisibleGroupsService(new StubSender(summaries), claimable, new StubAuth(signedInAs));
    }

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

    private sealed class StubLocalSyncStore(IReadOnlyList<SyncedGroupRecord> records) : ILocalSyncStore
    {
        public Task<IReadOnlyList<SyncedGroupRecord>> LoadGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(records);

        public Task SaveGroupsAsync(IReadOnlyList<SyncedGroupRecord> records, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SyncedProfileRecord?> LoadProfileAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveProfileAsync(SyncedProfileRecord profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SyncedPreferencesRecord?> LoadPreferencesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SavePreferencesAsync(SyncedPreferencesRecord preferences, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
