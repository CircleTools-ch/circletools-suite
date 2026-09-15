using CircleTools.SecretSanta.Ui;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="HandoutProgressService"/> (MNF-7, FR-045): per-event hand-out progress is
/// persisted and resumable, a member key is stable per link, and a re-draw (new links) resets progress.
/// </summary>
public sealed class HandoutProgressServiceTests
{
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly Guid Event = Guid.NewGuid();
    private static readonly string[] Draw = ["https://s/r#for=Al&d=1", "https://s/r#for=Bo&d=2", "https://s/r#for=Cy&d=3"];

    private readonly HandoutProgressService _service = new(new InMemoryKeyValueStore());

    [Fact]
    public async Task NothingHandedOut_Initially()
    {
        Assert.Empty(await _service.GetDoneAsync(Group, Event, Draw));
    }

    [Fact]
    public async Task MarksAndRemembers_AParticipant()
    {
        var key = HandoutProgressService.MemberKey(Draw[1]);

        await _service.SetDoneAsync(Group, Event, Draw, key, done: true);

        var done = await _service.GetDoneAsync(Group, Event, Draw);
        Assert.Contains(key, done);
        Assert.Single(done);
    }

    [Fact]
    public async Task ProgressPersists_AcrossANewServiceInstance_OnTheSameStore()
    {
        var store = new InMemoryKeyValueStore();
        var first = new HandoutProgressService(store);
        var key = HandoutProgressService.MemberKey(Draw[0]);
        await first.SetDoneAsync(Group, Event, Draw, key, done: true);

        // A fresh service over the same store (mirrors reopening the app) still sees the progress.
        var second = new HandoutProgressService(store);
        Assert.Contains(key, await second.GetDoneAsync(Group, Event, Draw));
    }

    [Fact]
    public async Task Unmarking_RemovesAParticipant()
    {
        var key = HandoutProgressService.MemberKey(Draw[0]);
        await _service.SetDoneAsync(Group, Event, Draw, key, done: true);

        await _service.SetDoneAsync(Group, Event, Draw, key, done: false);

        Assert.Empty(await _service.GetDoneAsync(Group, Event, Draw));
    }

    [Fact]
    public async Task ReDraw_ResetsProgress()
    {
        // Hand out everyone in the first draw.
        foreach (var url in Draw)
        {
            await _service.SetDoneAsync(Group, Event, Draw, HandoutProgressService.MemberKey(url), done: true);
        }

        Assert.Equal(3, (await _service.GetDoneAsync(Group, Event, Draw)).Count);

        // A re-draw mints new links; progress for the new draw starts empty.
        string[] redraw = ["https://s/r#for=Al&d=9", "https://s/r#for=Bo&d=8", "https://s/r#for=Cy&d=7"];
        Assert.Empty(await _service.GetDoneAsync(Group, Event, redraw));
    }

    [Fact]
    public void MemberKey_IsStablePerLink_AndDistinctBetweenLinks()
    {
        Assert.Equal(HandoutProgressService.MemberKey(Draw[0]), HandoutProgressService.MemberKey(Draw[0]));
        Assert.NotEqual(HandoutProgressService.MemberKey(Draw[0]), HandoutProgressService.MemberKey(Draw[1]));
    }

    private sealed class InMemoryKeyValueStore : IKeyValueStore
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
