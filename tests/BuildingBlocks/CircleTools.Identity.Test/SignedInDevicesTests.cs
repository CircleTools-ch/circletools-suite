using CircleTools.Identity.Tokens;

namespace CircleTools.Identity.Test;

/// <summary>
/// The list of signed-in devices and signing out one of them (ADR-0079 amendment, Q26): each live session
/// with a coarse device label, this one marked, and only the person's own sessions listed or ended.
/// </summary>
public sealed class SignedInDevicesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36", "Chrome · Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0", "Edge · Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15", "Safari · macOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari · iOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0", "Firefox · Linux")]
    [InlineData("Mozilla/5.0 (Linux; Android 15; Pixel 9 Pro) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0 Mobile Safari/537.36", "Chrome · Android")]
    [InlineData("CircleTools-App/0.1.118 (Wichteln; Android)", "Wichteln app · Android")]
    [InlineData("CircleTools-App/0.1 (Secret Santa; iOS)", "Secret Santa app · iOS")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("curl/8.4.0", "")]
    public void TheLabel_IsBrowserAndSystem_OrTheApp(string? userAgent, string expected) =>
        Assert.Equal(expected, DeviceLabel.FromUserAgent(userAgent));

    [Fact]
    public async Task TheList_ShowsEachLiveSession_WithThisDeviceFirstAndMarked()
    {
        var (service, _, clock, user) = Build();
        var phone = await service.StartAsync(user, "Wichteln app · Android");
        clock.Advance(TimeSpan.FromHours(1));
        var laptop = await service.StartAsync(user, "Chrome · Windows");

        var list = await service.ListAsync(user.Id, phone.RefreshSecret);

        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsCurrent);
        Assert.Equal("Wichteln app · Android", list[0].Device);
        Assert.Equal("Chrome · Windows", list[1].Device);
        Assert.False(list[1].IsCurrent);
        Assert.Equal(Now + TimeSpan.FromHours(1), list[1].SignedInAt);
        Assert.NotEqual(laptop.RefreshSecret, phone.RefreshSecret);
    }

    [Fact]
    public async Task SigningOutADevice_EndsOnlyThatSession_AndItsRefreshFails()
    {
        var (service, _, _, user) = Build();
        var phone = await service.StartAsync(user, "Wichteln app · Android");
        var laptop = await service.StartAsync(user, "Chrome · Windows");
        var laptopId = (await service.ListAsync(user.Id, laptop.RefreshSecret)).Single(s => s.IsCurrent).Id;

        Assert.True(await service.RevokeSessionByIdAsync(user.Id, laptopId));

        Assert.IsType<SessionRefreshResult.Failed>(await service.RefreshAsync(laptop.RefreshSecret));
        Assert.IsType<SessionRefreshResult.Succeeded>(await service.RefreshAsync(phone.RefreshSecret));
        Assert.Single(await service.ListAsync(user.Id, null));
    }

    [Fact]
    public async Task SomeoneElsesSession_CanNeitherBeListedNorEnded()
    {
        var (service, _, users, owner, intruder) = BuildTwo();
        var owners = await service.StartAsync(owner, "Chrome · Windows");
        var ownersId = (await service.ListAsync(owner.Id, owners.RefreshSecret)).Single().Id;

        Assert.Empty(await service.ListAsync(intruder.Id, owners.RefreshSecret));
        Assert.False(await service.RevokeSessionByIdAsync(intruder.Id, ownersId));
        Assert.IsType<SessionRefreshResult.Succeeded>(await service.RefreshAsync(owners.RefreshSecret));
        Assert.NotNull(users);
    }

    [Fact]
    public void AnOverlongLabel_IsCut()
    {
        var chain = RefreshTokenChain.Start(Guid.NewGuid(), "handle", Now, new TokenLifetimeOptions(), new string('x', 300));

        Assert.Equal(RefreshTokenChain.MaxDeviceLength, chain.Device.Length);
    }

    private static (SessionService Service, FakeChainStore Chains, TestClock Clock, CircleToolsUser User) Build()
    {
        var clock = new TestClock(Now);
        var chains = new FakeChainStore();
        var users = new FakeUserLookup();
        var user = TestFactory.User();
        users.Add(user);
        return (TestFactory.Session(chains, users, clock), chains, clock, user);
    }

    private static (SessionService Service, FakeChainStore Chains, FakeUserLookup Users, CircleToolsUser Owner, CircleToolsUser Intruder) BuildTwo()
    {
        var clock = new TestClock(Now);
        var chains = new FakeChainStore();
        var users = new FakeUserLookup();
        var owner = TestFactory.User();
        var intruder = TestFactory.User();
        intruder.Id = Guid.NewGuid();
        users.Add(owner);
        users.Add(intruder);
        return (TestFactory.Session(chains, users, clock), chains, users, owner, intruder);
    }
}
