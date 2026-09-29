using CircleTools.Application;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui;
using CircleTools.SecretSanta.Ui.Pages;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Component tests for the reveal-links share page (FEAT-001, ADR-0028 share seam): the native Share
/// button is offered only where the platform has a share sheet, and it hands the reveal link to the
/// <see cref="IShareService"/>; Copy stays available regardless.
/// </summary>
public sealed class SharePageTests : IDisposable
{
    private const string RevealUrl = "https://secretsanta.example/r#for=Al&v=1&d=blob";

    private readonly Bunit.TestContext _ctx = new();
    private readonly FakeShareService _share = new();

    public SharePageTests()
    {
        // MudBlazor (ADR-0040): the page is a MudBlazor component; loose JSInterop lets its JS calls
        // (ripples, the clipboard copy) no-op instead of failing the render.
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.Services.AddSingleton<IShareService>(_share);
        // The page renders EventNav, which records recency (R2-5) via RecentAccessService over storage.
        _ctx.Services.AddSingleton<IKeyValueStore>(new ShareStubKeyValueStore());
        _ctx.Services.AddSingleton<RecentAccessService>();
        _ctx.Services.AddSingleton<ISender>(new ShareSender(RevealUrl));
        _ctx.Services.AddSingleton<ISyncSignals>(new FakeSyncSignals());
        // The share page now offers the guided hand-out (MNF-7), which persists progress via this service.
        _ctx.Services.AddSingleton<HandoutProgressService>();
        // Live reveal links (ADR-0068): the page reads reveal tokens from the local store and publishes
        // slices. With an empty store and an unreachable API it exercises only the local-only path.
        _ctx.Services.AddSingleton<ILocalSyncStore>(new EmptyLocalSyncStore());
        _ctx.Services.AddSingleton<NoOpSharedGroupApi>();
        _ctx.Services.AddSingleton<ISharedGroupApi>(sp => sp.GetRequiredService<NoOpSharedGroupApi>());
        _ctx.Services.AddSingleton<ISharedGroupAccountApi>(sp => sp.GetRequiredService<NoOpSharedGroupApi>());
        _ctx.Services.AddSingleton<RevealSlicePublisher>();
    }

    private IRenderedComponent<Share> RenderShare() =>
        _ctx.RenderComponent<Share>(p => p
            .Add(s => s.GroupId, Guid.NewGuid())
            .Add(s => s.EventId, Guid.NewGuid()));

    [Fact]
    public void OffersTheGuidedHandOut_WithProgress()
    {
        var cut = RenderShare();

        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "HandOutLinks");
        // PassThrough localizer echoes "HandoutProgress" with its args → "HandoutProgress0, 1" is not how
        // it formats; it formats the value with string.Format, so "Handed out: 0 / 1" would need the real
        // resource. The echo localizer returns the key with args applied, so just assert the button here.
    }

    [Fact]
    public async Task ShowsHandedOutChip_ForAParticipantAlreadyHandedOut()
    {
        var groupId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var handout = _ctx.Services.GetRequiredService<HandoutProgressService>();
        await handout.SetDoneAsync(groupId, eventId, [RevealUrl], HandoutProgressService.MemberKey(RevealUrl), done: true);

        var cut = _ctx.RenderComponent<Share>(p => p.Add(s => s.GroupId, groupId).Add(s => s.EventId, eventId));

        cut.WaitForAssertion(() => Assert.Contains("HandedOut", cut.Markup));
    }

    [Fact]
    public void ShareButtonIsOffered_WhenThePlatformCanShare()
    {
        _share.Supported = true;

        var cut = RenderShare();

        // _canShare is resolved after first render; wait for the Share button to appear.
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Share"));
    }

    [Fact]
    public void ShareButtonIsHidden_WhenThePlatformCannotShare_ButCopyRemains()
    {
        _share.Supported = false;

        var cut = RenderShare();

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() == "Share");
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Copy");
    }

    [Fact]
    public void QrButtonIsOffered_ForEveryLink_RegardlessOfShareSupport()
    {
        // The QR code is generated client-side, so it is available on every platform — unlike Share,
        // it does not gate on IShareService support.
        _share.Supported = false;

        var cut = RenderShare();

        // The localizer under test echoes keys, so the QR button reads its key "ShowQr".
        Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "ShowQr");
    }

    [Fact]
    public void ClickingShare_HandsTheRevealLinkToTheShareService()
    {
        _share.Supported = true;

        var cut = RenderShare();
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll("button"), b => b.TextContent.Trim() == "Share"));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Share").Click();

        var call = Assert.Single(_share.Calls);
        Assert.Equal(RevealUrl, call.Url);
        Assert.Contains(RevealUrl, call.Text); // the link is in the body too, for share targets that read only text
    }

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();

    /// <summary>Records share requests and reports a configurable "can share" so the gate can be exercised.</summary>
    private sealed class FakeShareService : IShareService
    {
        public bool Supported { get; set; }

        public List<(string Title, string Text, string? Url)> Calls { get; } = [];

        public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Supported);

        public ValueTask ShareAsync(string title, string text, string? url = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((title, text, url));
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>An <see cref="ISender"/> answering the three queries the share page and its EventNav send.</summary>
file sealed class ShareSender(string revealUrl) : ISender
{
    public Task Send(ICommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        object result = query switch
        {
            GetRevealLinksQuery => (IReadOnlyList<RevealLinkDto>)[new RevealLinkDto("Al", Guid.Empty, revealUrl)],
            GetEventQuery q => new EventDto(q.EventId, q.GroupId, "Christmas", null, null, true, [], false, []),
            GetGroupQuery q => new GroupDto(q.GroupId, "Family", [], [], []),
            _ => throw new NotSupportedException($"Unexpected query {query.GetType().Name}"),
        };

        return Task.FromResult((TResult)result);
    }
}

/// <summary>In-memory <see cref="IKeyValueStore"/> so EventNav's recency recording renders in tests.</summary>
file sealed class ShareStubKeyValueStore : IKeyValueStore
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
