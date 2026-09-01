using System.Security.Claims;
using CircleTools.Application;
using CircleTools.Circles.Contracts;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.Ui.Abstractions;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
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

/// <summary>Unit tests for applying member-link resolutions onto local groups (FEAT-005).</summary>
public sealed class LinkResolutionApplierTests
{
    [Fact]
    public async Task ApplyPending_SetsUserIdOnALinkedResolution_AndAcknowledgesIt()
    {
        var repository = new InMemoryGroupRepository();
        var group = Group.Create("Family");
        var aliceId = group.AddMember("Alice");
        await repository.SaveAsync(group, default);

        var userId = Guid.NewGuid();
        var resolutionId = Guid.NewGuid();
        var api = new FakeLinkingApi
        {
            Resolutions = new MemberLinkResolutionsResponse(
                [new MemberLinkResolution(resolutionId, group.Id, aliceId, MemberLinkResolutionKind.Linked, userId)]),
        };

        var applied = await new LinkResolutionApplier(api, repository).ApplyPendingAsync();

        Assert.Equal(1, applied);
        Assert.Contains(resolutionId, api.Acked);
        var reloaded = await repository.GetAsync(group.Id, default);
        Assert.Equal(userId, reloaded!.FindMember(aliceId)!.UserId);
    }

    [Fact]
    public async Task ApplyPending_ClearsUserIdOnAnUnlinkResolution()
    {
        var repository = new InMemoryGroupRepository();
        var group = Group.Create("Family");
        var aliceId = group.AddMember("Alice");
        var userId = Guid.NewGuid();
        group.LinkMember(aliceId, userId);
        await repository.SaveAsync(group, default);

        var api = new FakeLinkingApi
        {
            Resolutions = new MemberLinkResolutionsResponse(
                [new MemberLinkResolution(Guid.NewGuid(), group.Id, aliceId, MemberLinkResolutionKind.Unlinked, null)]),
        };

        await new LinkResolutionApplier(api, repository).ApplyPendingAsync();

        var reloaded = await repository.GetAsync(group.Id, default);
        Assert.Null(reloaded!.FindMember(aliceId)!.UserId);
    }
}

/// <summary>Component tests for the signed-in assignment view (FR-029).</summary>
public sealed class MyAssignmentsPageTests : IDisposable
{
    private const string RecipientName = "Recipient-Zoe";
    private readonly Bunit.TestContext _ctx = new();

    public MyAssignmentsPageTests()
    {
        // MudBlazor (ADR-0040): the page is a MudBlazor component now; loose JSInterop lets its JS
        // calls (ripples/element refs) no-op instead of failing the render.
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.AddTestAuthorization().SetAuthorized("member");
    }

    [Fact]
    public void TheRecipientIsHiddenBehindADeliberateRevealStep()
    {
        var api = new FakeLinkingApi
        {
            Assignments = new MyAssignmentsResponse(
                [new MyAssignmentResponse(Guid.NewGuid(), "Family", Guid.NewGuid(), "Christmas", RecipientName, ["Book"], null, null)]),
        };
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        var cut = _ctx.RenderComponent<MyAssignments>();

        // Before revealing, the recipient must not appear anywhere in the markup.
        Assert.DoesNotContain(RecipientName, cut.Markup);

        cut.Find("button").Click();

        Assert.Contains(RecipientName, cut.Markup);
    }

    [Fact]
    public void PastOccasionsAreHidden_FutureAndUndatedStay()
    {
        _ctx.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero)));

        var past = new EventDetailsDto(new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero), "", "");
        var future = new EventDetailsDto(new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero), "", "");
        var api = new FakeLinkingApi
        {
            Assignments = new MyAssignmentsResponse(
            [
                new MyAssignmentResponse(Guid.NewGuid(), "PastGroup", Guid.NewGuid(), "NewYear", "R1", ["x"], null, past),
                new MyAssignmentResponse(Guid.NewGuid(), "FutureGroup", Guid.NewGuid(), "Christmas", "R2", ["x"], null, future),
                new MyAssignmentResponse(Guid.NewGuid(), "UndatedGroup", Guid.NewGuid(), "Someday", "R3", ["x"], null, null),
            ]),
        };
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        var cut = _ctx.RenderComponent<MyAssignments>();

        Assert.DoesNotContain("PastGroup", cut.Markup);
        Assert.Contains("FutureGroup", cut.Markup);
        Assert.Contains("UndatedGroup", cut.Markup);
    }

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}

/// <summary>Component tests for the signed-in wishlist page (FR-030): past occasions get no slot.</summary>
public sealed class MyWishlistsPageTests : IDisposable
{
    private readonly Bunit.TestContext _ctx = new();

    public MyWishlistsPageTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.AddTestAuthorization().SetAuthorized("member");
    }

    [Fact]
    public void PastOccasionsGetNoWishlistSlot_FutureAndUndatedStay()
    {
        _ctx.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero)));

        var past = new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero);
        var future = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);
        var api = new FakeLinkingApi
        {
            Memberships = new MyMembershipsResponse(
            [
                new MyMembership(Guid.NewGuid(), "PastGroup", Guid.NewGuid(), "NewYear", past),
                new MyMembership(Guid.NewGuid(), "FutureGroup", Guid.NewGuid(), "Christmas", future),
                new MyMembership(Guid.NewGuid(), "UndatedGroup", Guid.NewGuid(), "Someday", null),
            ]),
        };
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        var cut = _ctx.RenderComponent<MyWishlists>();

        Assert.DoesNotContain("PastGroup", cut.Markup);
        Assert.Contains("FutureGroup", cut.Markup);
        Assert.Contains("UndatedGroup", cut.Markup);
    }

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}

/// <summary>
/// The member-link page's honest-feedback guard: linking your own member to your own still-unverified
/// address must not silently "send a request" (the server matches verified addresses only) — it says
/// verify first and makes no server call.
/// </summary>
public sealed class MembersPageLinkGuardTests : IDisposable
{
    private readonly Bunit.TestContext _ctx = new();

    public MembersPageLinkGuardTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        // The Members page renders GroupNav, which records recency (R2-5) via RecentAccessService.
        _ctx.Services.AddSingleton<IKeyValueStore>(new StubKeyValueStore());
        _ctx.Services.AddSingleton<RecentAccessService>();
    }

    [Fact]
    public void OwnMemberToOwnUnverifiedAddress_ShowsVerifyMessage_AndMakesNoServerCall()
    {
        var groupId = Guid.NewGuid();
        var group = new GroupDto(groupId, "Familie", [new MemberDto(Guid.NewGuid(), "Anna")], [], []);
        _ctx.Services.AddSingleton<ISender>(new GroupSender(group));

        var api = new FakeLinkingApi();
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        // Signed in, address NOT verified (no email_verified claim).
        var auth = _ctx.AddTestAuthorization();
        auth.SetAuthorized("Adrian");
        auth.SetClaims(new Claim(ClaimTypes.Email, "adrian@test.ch"));

        var cut = _ctx.RenderComponent<Members>(p => p.Add(m => m.GroupId, groupId));

        ClickButton(cut, "MemberLink");                          // open the link box
        cut.Find("input[type=email]").Input("adrian@test.ch");   // our own address
        ClickButton(cut, "MemberLinkSend");

        Assert.Contains("MemberLinkVerifyEmailFirst", cut.Markup);
        Assert.Empty(api.SentLinks); // short-circuited before any server call
    }

    // Exact match: the roster now also has a "MemberLinkWithMyAccount" button whose key contains
    // "MemberLink", so a Contains match would be ambiguous.
    private static void ClickButton(IRenderedComponent<Members> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text).Click();

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}

/// <summary>
/// The one-click self-link ("Link with my account") and the send-time duplicate refusal (ADR-0044):
/// the self-link is offered only while my account backs no member of the group, and an address already
/// linked here is reported as an error rather than a silent "request sent".
/// </summary>
public sealed class MembersPageSelfLinkTests : IDisposable
{
    private readonly Bunit.TestContext _ctx = new();
    private readonly Guid _myUserId = Guid.NewGuid();

    public MembersPageSelfLinkTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        // The Members page renders GroupNav, which records recency (R2-5) via RecentAccessService.
        _ctx.Services.AddSingleton<IKeyValueStore>(new StubKeyValueStore());
        _ctx.Services.AddSingleton<RecentAccessService>();
    }

    [Fact]
    public void OneClickSelfLink_IsOfferedWhenUnlinked_AndCallsTheSelfLinkSurface()
    {
        var groupId = Guid.NewGuid();
        var member = new MemberDto(Guid.NewGuid(), "Anna");
        SignInWithGroup(groupId, member, out var api);

        var cut = _ctx.RenderComponent<Members>(p => p.Add(m => m.GroupId, groupId));

        ClickButton(cut, "MemberLinkWithMyAccount");

        var self = Assert.Single(api.SelfLinks);
        Assert.Equal(member.Id, self.MemberId);
        Assert.Contains("MemberLinkSelfLinked", cut.Markup);
    }

    [Fact]
    public void SelfLink_IsHiddenOnceMyAccountBacksAMemberOfTheGroup()
    {
        var groupId = Guid.NewGuid();
        // I already back this member: the self-link affordance must be gone everywhere (only invite remains).
        var member = new MemberDto(Guid.NewGuid(), "Anna", _myUserId);
        SignInWithGroup(groupId, member, out _);

        var cut = _ctx.RenderComponent<Members>(p => p.Add(m => m.GroupId, groupId));

        Assert.DoesNotContain("MemberLinkWithMyAccount", cut.Markup);
    }

    [Fact]
    public void InvitingAnAddressAlreadyLinkedHere_ShowsAnError()
    {
        var groupId = Guid.NewGuid();
        var member = new MemberDto(Guid.NewGuid(), "Anna");
        SignInWithGroup(groupId, member, out var api);
        api.SendOutcome = MemberLinkOutcome.AlreadyLinkedInGroup;

        var cut = _ctx.RenderComponent<Members>(p => p.Add(m => m.GroupId, groupId));

        ClickButton(cut, "MemberLink");
        cut.Find("input[type=email]").Input("someone-else@test.ch");
        ClickButton(cut, "MemberLinkSend");

        Assert.Contains("MemberLinkAlreadyInGroup", cut.Markup);
    }

    private void SignInWithGroup(Guid groupId, MemberDto member, out FakeLinkingApi api)
    {
        var group = new GroupDto(groupId, "Familie", [member], [], []);
        _ctx.Services.AddSingleton<ISender>(new GroupSender(group));

        api = new FakeLinkingApi();
        _ctx.Services.AddSingleton<CircleTools.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        var auth = _ctx.AddTestAuthorization();
        auth.SetAuthorized("Adrian");
        // A verified address and the account id the "my account" checks read (ClaimTypes.NameIdentifier).
        auth.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, _myUserId.ToString()),
            new Claim(ClaimTypes.Email, "adrian@test.ch"),
            new Claim("email_verified", "true"));
    }

    private static void ClickButton(IRenderedComponent<Members> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text).Click();

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}

/// <summary>An <see cref="ISender"/> that answers <see cref="GetGroupQuery"/> with one fixed group.</summary>
file sealed class GroupSender(GroupDto group) : ISender
{
    public Task Send(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default) =>
        Task.FromResult((TResult)(object)group);
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

/// <summary>Unit test for the client event-registry publish: the event date is carried (FR-030).</summary>
public sealed class AssignmentPublisherTests
{
    [Fact]
    public async Task PublishAsync_CarriesTheEventDateIntoTheRegistry()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.LinkMember(alice, Guid.NewGuid()); // at least one linked member, else publish is a no-op
        var eventId = group.AddEvent("Christmas");
        var date = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);
        group.SetEventDetails(eventId, EventDetails.Create(date, "Zurich", "Bring snacks"));

        var api = new FakeLinkingApi();
        await new AssignmentPublisher(api).PublishAsync(group);

        var published = Assert.Single(api.PublishedEvents);
        var entry = Assert.Single(published.Request.Events);
        Assert.Equal(date, entry.Date);
    }
}
