// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using CircleTools.Application;
using CircleTools.ApiClient;
using CircleTools.Circles.Contracts;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.Ui.Abstractions;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>Unit tests for the create-from-circle DTO → member-input mapping (FR-027).</summary>
public sealed class CreateFromCircleMappingTests
{
    [Fact]
    public void AnAccountedPersonBecomesLinked_AManagedMemberBecomesUnlinked()
    {
        var accountId = Guid.NewGuid();
        var accounted = new CirclePersonResponse(Guid.NewGuid(), "Alice", "member", IsManaged: false, IsYou: false, AccountId: accountId);
        var managed = new CirclePersonResponse(Guid.NewGuid(), "Child", "member", IsManaged: true, IsYou: false, AccountId: null);

        var linked = CreateFromCircleService.ToInput(accounted);
        var unlinked = CreateFromCircleService.ToInput(managed);

        Assert.Equal(accountId, linked.UserId);
        Assert.Equal(accounted.MembershipId, linked.CirclePersonId); // origin recorded
        Assert.Null(unlinked.UserId);
    }
}

/// <summary>
/// The members page brings people into an online group (ADR-0086): "This is me", an email invitation and freeing a
/// spot are all the same participant underneath, offered only where this device may manage participants; a group
/// kept on this device says how to let people take part instead.
/// </summary>
public sealed class MembersPageParticipantTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();
    private readonly IKeyValueStore _keyValues = new StubKeyValueStore();
    private readonly FakeOnlineGroupApi _api = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _anna = Guid.NewGuid();
    private IRenderedComponent<MudBlazor.MudPopoverProvider>? _popover;

    public MembersPageParticipantTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IKeyValueStore>(_keyValues);
        _ctx.Services.AddSingleton<RecentAccessService>();
        _ctx.Services.AddSingleton<ISyncSignals>(new FakeSyncSignals());
        _ctx.Services.AddSingleton<IOnlineGroupApi>(_api);
        _ctx.Services.AddSingleton<ISender>(new GroupSender(new GroupDto(_groupId, "Familie", [new MemberDto(_anna, "Anna")], [], [])));
    }

    [Fact]
    public void AGroupOnThisDevice_SaysHowToLetPeopleIn_AndOffersNoInvite()
    {
        Use(OnlineRecords.OnDevice(_groupId));
        _ctx.AddAuthorization().SetAuthorized("Adrian");

        var cut = Render();

        Assert.Contains("MembersOnlineHint", cut.Markup);
        Assert.DoesNotContain("MemberLink", MenuItems(cut));
    }

    [Fact]
    public void ThisIsMe_ConnectsTheOrganizersOwnMember()
    {
        Use(OnlineRecords.Organized(_groupId));
        var auth = _ctx.AddAuthorization();
        auth.SetAuthorized("Adrian");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        var cut = Render();
        PickFromMenu(cut, "MemberLinkWithMyAccount");

        cut.WaitForAssertion(() => Assert.Single(_api.Added));
        Assert.Equal((_groupId, new AddParticipantRequest(_anna, ParticipantRoute.OwnMember)), _api.Added[0]);
    }

    [Fact]
    public async Task AnInvitation_IsSent_AndAnAccountAlreadyInTheGroupIsReported()
    {
        Use(OnlineRecords.Organized(_groupId));
        var auth = _ctx.AddAuthorization();
        auth.SetAuthorized("Adrian");
        auth.SetClaims(new Claim(ClaimTypes.Email, "adrian@test.ch"));
        _api.AddFailure = ApiFailure.Conflict;

        var cut = Render();
        PickFromMenu(cut, "MemberLink");
        await cut.InvokeAsync(() => cut.Find("input[type=email]").Input("ben@test.ch"));
        await cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Trim() == "MemberLinkSend").Click());

        cut.WaitForAssertion(() => Assert.Contains("MemberLinkAlreadyInGroup", cut.Markup));
        Assert.Equal(new AddParticipantRequest(_anna, ParticipantRoute.Invitation, "ben@test.ch"), _api.Added.Single().Request);
    }

    [Fact]
    public void ATakenMember_ShowsTheBadge_AndOffersOnlyToFreeTheSpot()
    {
        Use(OnlineRecords.Organized(_groupId, roles:
        [
            new GroupRoleView("owner", "account", null, false, "created"),
            new GroupRoleView("participant", "deviceKey", _anna, false, "joinLink"),
        ]));
        _ctx.AddAuthorization().SetAuthorized("Adrian");

        var cut = Render();

        Assert.Contains("MemberConnectedBadge", cut.Markup);
        var items = MenuItems(cut);
        Assert.Contains("ReleaseMember", items);
        Assert.DoesNotContain("MemberLink", items);
    }

    [Fact]
    public async Task FreeingASpot_ShowsTheMemberAsFree_RightAway()
    {
        Use(OnlineRecords.Organized(_groupId, roles:
        [
            new GroupRoleView("owner", "account", null, false, "created"),
            new GroupRoleView("participant", "deviceKey", _anna, false, "joinLink"),
        ]));
        _ctx.AddAuthorization().SetAuthorized("Adrian");
        _api.View = new OrganizerGroupView(_groupId, 1, 1, System.Text.Json.JsonDocument.Parse("{}").RootElement, false,
            [new GroupRoleView("owner", "account", null, false, "created")], [], [OnlinePermissions.ManageParticipants], new GroupLinks(null, null));
        var dialogs = _ctx.Render<MudBlazor.MudDialogProvider>();

        var cut = Render();
        Assert.Contains("MemberConnectedBadge", cut.Markup);
        PickFromMenu(cut, "ReleaseMember");
        dialogs.WaitForAssertion(() => Assert.Contains(dialogs.FindAll("button"), b => b.TextContent.Trim() == "ReleaseMemberConfirmYes"));
        await dialogs.InvokeAsync(() => dialogs.FindAll("button").First(b => b.TextContent.Trim() == "ReleaseMemberConfirmYes").Click());

        // The action runs after the menu closes, outside the click handler; it still has to re-render the roster.
        cut.WaitForAssertion(() => Assert.DoesNotContain("MemberConnectedBadge", cut.Markup));
        Assert.Equal((_groupId, _anna), Assert.Single(_api.Released));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private void Use(GroupRecord record)
    {
        var store = OnlineRecords.Seed(_keyValues, record);
        _ctx.Services.AddSingleton<IGroupRecordStore>(store);
        _ctx.Services.AddSingleton(new OnlineGroupSync(store, _api));
    }

    private IRenderedComponent<Members> Render() => _ctx.Render<Members>(p => p.Add(m => m.GroupId, _groupId));

    // The row's actions live in its overflow menu, rendered into the popover provider.
    private IReadOnlyList<string> MenuItems(IRenderedComponent<Members> cut)
    {
        _popover ??= _ctx.Render<MudBlazor.MudPopoverProvider>();
        cut.InvokeAsync(() => cut.FindAll("button").First(b => b.GetAttribute("aria-label") == "MoreActionsFor").Click()).GetAwaiter().GetResult();
        _popover.WaitForAssertion(() => Assert.NotEmpty(_popover.FindAll(".mud-menu-item")));
        return [.. _popover.FindAll(".mud-menu-item").Select(i => i.TextContent.Trim())];
    }

    private void PickFromMenu(IRenderedComponent<Members> cut, string text)
    {
        Assert.Contains(text, MenuItems(cut));
        var popover = _popover!;
        popover.InvokeAsync(() => popover.FindAll(".mud-menu-item").First(i => i.TextContent.Trim() == text).Click()).GetAwaiter().GetResult();
    }
}

/// <summary>A background sync that pulled a co-organizer's change re-renders the roster in place.</summary>
public sealed class MembersPageSyncRefreshTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();

    public MembersPageSyncRefreshTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        var keyValues = new StubKeyValueStore();
        _ctx.Services.AddSingleton<IKeyValueStore>(keyValues);
        _ctx.Services.AddSingleton<RecentAccessService>();
        var store = OnlineRecords.Seed(keyValues);
        _ctx.Services.AddSingleton<IGroupRecordStore>(store);
        _ctx.Services.AddSingleton(new OnlineGroupSync(store, new FakeOnlineGroupApi()));
    }

    [Fact]
    public void Roster_RefreshesInPlace_WhenABackgroundSyncCompletes()
    {
        var groupId = Guid.NewGuid();
        var sender = new MutableGroupSender(
            new GroupDto(groupId, "Familie", [new MemberDto(Guid.NewGuid(), "Anna")], [], []));
        _ctx.Services.AddSingleton<ISender>(sender);

        var signals = new FakeSyncSignals();
        _ctx.Services.AddSingleton<ISyncSignals>(signals);

        // An account-free co-organizer (the reported scenario is signed out).
        _ctx.AddAuthorization().SetNotAuthorized();

        var cut = _ctx.Render<Members>(p => p.Add(m => m.GroupId, groupId));
        Assert.Contains("Anna", cut.Markup);
        Assert.DoesNotContain("Bruno", cut.Markup);

        // A co-organizer's add is pulled into the local store; the background cycle then signals completion.
        sender.Group = new GroupDto(groupId, "Familie",
            [new MemberDto(Guid.NewGuid(), "Anna"), new MemberDto(Guid.NewGuid(), "Bruno")], [], []);
        cut.InvokeAsync(signals.RaiseSyncCompleted);

        // The page reloads itself — no manual refresh needed.
        cut.WaitForAssertion(() => Assert.Contains("Bruno", cut.Markup));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}

/// <summary>An <see cref="ISender"/> that answers <see cref="CircleTools.SecretSanta.Core.Application.GetGroupQuery"/> with one fixed group.</summary>
file sealed class GroupSender(GroupDto group) : ISender
{
    public Task Send(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Task.FromResult((TResult)(object)group);
}

// Like GroupSender, but the answered group can be swapped mid-test to model a background pull.
file sealed class MutableGroupSender(GroupDto group) : ISender
{
    public GroupDto Group { get; set; } = group;

    public Task Send(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Task.FromResult((TResult)(object)Group);
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> so components that record recency (GroupNav → RecentAccessService) render in tests.</summary>
file sealed class StubKeyValueStore : IKeyValueStore
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
