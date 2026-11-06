using System.Globalization;
using System.Security.Claims;
using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Account.Auth;
using CircleTools.Ui.Shared;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// The join link (ADR-0086, FR-041) opened on a device that already holds the group offers to open it instead of
/// joining again, which would give the same person a second spot (S24 device test, 2026-10-25).
/// </summary>
public sealed class JoinPageTests : IAsyncDisposable
{
    private const string StoreUrl = "https://apps.apple.com/app/id6817309711";

    private readonly Bunit.BunitContext _ctx = new();
    private readonly IKeyValueStore _keyValues = new StubKeyValueStore();
    private readonly FakeOnlineGroupApi _api = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _account = Guid.NewGuid();

    public JoinPageTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IStringLocalizer<SharedStrings>>(new PassThroughStringLocalizer<SharedStrings>());
        _ctx.Services.AddSingleton<IKeyValueStore>(_keyValues);
        _ctx.Services.AddSingleton<IOnlineGroupApi>(_api);
        _ctx.Services.AddSingleton<PendingJoinLink>();
        _ctx.Services.AddSingleton<IAccessTokenStore>(new InMemoryAccessTokenStore(TimeProvider.System));
        _ctx.Services.AddSingleton<IRefreshTokenStore>(new RefreshTokenStore(_keyValues, new RefreshTokenStoreOptions("test.refresh")));
        _ctx.Services.AddSingleton<SessionAccount>();
        _api.Preview = new JoinPreview(_groupId, "Olgas Runde",
            [new JoinableMember(Guid.NewGuid(), "Kim", Taken: true), new JoinableMember(Guid.NewGuid(), "Lou", Taken: false)]);

        var auth = _ctx.AddAuthorization();
        auth.SetAuthorized("Sami");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, _account.ToString()));
    }

    [Fact]
    public void AGroupThisAccountTakesPartIn_OffersToOpenIt_NotToJoinAgain()
    {
        Use(new GroupRecord(_groupId, GroupPlace.Online, null,
            new ParticipantView(_groupId, "Olgas Runde", 1, Guid.NewGuid(), "Kim", null, [], []), 1, false,
            new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: _account)));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-already-in")));
        Assert.Contains("JoinAlreadyInAs", cut.Markup);
        Assert.Empty(cut.FindAll("#join-panel"));
    }

    [Fact]
    public void AGroupHeldOnlyByAnotherAccount_StillOffersToJoin()
    {
        Use(new GroupRecord(_groupId, GroupPlace.Online, null,
            new ParticipantView(_groupId, "Olgas Runde", 1, Guid.NewGuid(), "Kim", null, [], []), 1, false,
            new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: Guid.NewGuid())));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
        Assert.Empty(cut.FindAll("#join-already-in"));
    }

    [Fact]
    public void ASecondJoinLink_WhileTheJoinPageIsOpen_ShowsTheNewGroup()
    {
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));
        var cut = Render();
        cut.WaitForAssertion(() => Assert.Contains("Olgas Runde", cut.Markup));

        // The app link handler navigates to the same route with a new fragment (and a per-link query).
        _api.Preview = new JoinPreview(Guid.NewGuid(), "Umas Runde", [new JoinableMember(Guid.NewGuid(), "Vera", Taken: false)]);
        _ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/join?open=2#t=other");

        cut.WaitForAssertion(() => Assert.Contains("Umas Runde", cut.Markup));
        Assert.DoesNotContain("Olgas Runde", cut.Markup);
    }

    [Fact]
    public void AnInvitedMember_IsMarkedInvited_NotJoined()
    {
        // ST-3 (2026-10-28): the invitee had not accepted yet, but the chip said "Already joined".
        _api.Preview = new JoinPreview(_groupId, "Olgas Runde",
            [new JoinableMember(Guid.NewGuid(), "Kim", Taken: true, Invited: true), new JoinableMember(Guid.NewGuid(), "Lou", Taken: false)]);
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.Contains("MemberInvitedBadge", cut.Find("#join-members").TextContent));
        Assert.DoesNotContain("JoinAlreadyTaken", cut.Find("#join-members").TextContent);
    }

    [Fact]
    public void AGroupWithNoMembersYet_DoesNotSayEveryoneHasJoined()
    {
        _api.Preview = new JoinPreview(_groupId, "Olgas Runde", []);
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
        Assert.Empty(cut.FindAll("#join-all-taken"));
    }

    [Fact]
    public void AGroupWhereEveryoneHasJoined_SaysSo()
    {
        _api.Preview = new JoinPreview(_groupId, "Olgas Runde", [new JoinableMember(Guid.NewGuid(), "Kim", Taken: true)]);
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-all-taken")));
    }

    [Fact]
    public void TheJoinForm_OnTheWeb_OffersTheConfiguredAppStoreLink()
    {
        _ctx.Services.AddSingleton(new AppStoreLink("ios", StoreUrl));
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
        var badge = cut.Find($"a[href='{StoreUrl}'] img");
        Assert.Contains("store-badges/app-store-", badge.GetAttribute("src"));
        Assert.Equal("GetTheApp_Ios", badge.GetAttribute("alt"));
    }

    [Theory]
    [InlineData("de-CH", "app-store-de.svg")]
    [InlineData("fr-CH", "app-store-fr.svg")]
    [InlineData("en", "app-store-en.svg")]
    public void TheAppStoreBadge_FollowsTheLanguage(string culture, string file)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            _ctx.Services.AddSingleton(new AppStoreLink("ios", StoreUrl));
            Use(OnlineRecords.OnDevice(Guid.NewGuid()));

            var cut = Render();

            cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
            Assert.EndsWith(file, cut.Find($"a[href='{StoreUrl}'] img").GetAttribute("src"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void ATestFlightLink_StaysAPlainButton_NotTheStoreBadge()
    {
        // The badge says "Download on the App Store", which a beta is not.
        _ctx.Services.AddSingleton(new AppStoreLink("testflight", "https://testflight.apple.com/join/x", "Join the beta"));
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
        Assert.Contains("Join the beta", cut.Markup);
        Assert.Empty(cut.FindAll("a[href^='https://testflight'] img"));
    }

    [Fact]
    public void TheJoinForm_InTheNativeApp_HasNoStoreLink()
    {
        // The MAUI heads register no AppStoreLink (ADR-0065), so the section renders nothing there.
        Use(OnlineRecords.OnDevice(Guid.NewGuid()));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-panel")));
        Assert.DoesNotContain("GetTheApp_Heading", cut.Markup);
    }

    [Fact]
    public void AGroupAlreadyOnThisDevice_DoesNotPromoteTheApp()
    {
        _ctx.Services.AddSingleton(new AppStoreLink("ios", StoreUrl));
        Use(new GroupRecord(_groupId, GroupPlace.Online, null,
            new ParticipantView(_groupId, "Olgas Runde", 1, Guid.NewGuid(), "Kim", null, [], []), 1, false,
            new OnlineAccess(AccessPrincipal.Account, "participant", [], AccountId: _account)));

        var cut = Render();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#join-already-in")));
        Assert.DoesNotContain(StoreUrl, cut.Markup);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();

    private void Use(GroupRecord record)
    {
        var store = OnlineRecords.Seed(_keyValues, record);
        _ctx.Services.AddSingleton<IGroupRecordStore>(store);
        _ctx.Services.AddSingleton(new OnlineGroupSync(store, _api));
    }

    private IRenderedComponent<Join> Render()
    {
        _ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/join#t=token");
        return _ctx.Render<Join>();
    }
}

/// <summary>An in-memory <see cref="IKeyValueStore"/> for the records and the refresh token.</summary>
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
