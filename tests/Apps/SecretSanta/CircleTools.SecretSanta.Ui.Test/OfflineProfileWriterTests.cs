using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Account.Api;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Tests for <see cref="OfflineProfileWriter"/> — the Secret-Santa profile writer that closes FR-012's
/// carried offline-language criterion (ADR-0035). A profile edit made offline is queued locally and
/// pushed to the authoritative <c>/profile</c> endpoint on the next sync; a change made on another
/// device is adopted here.
/// </summary>
public sealed class OfflineProfileWriterTests : IDisposable
{
    private readonly Guid _account = Guid.NewGuid();
    private readonly AccountApiStub _stub = new();
    private readonly LocalSyncStore _local = new(new FakeKeyValueStore());
    private readonly RecordingCultureProvider _culture = new("de-CH");

    /// <inheritdoc />
    public void Dispose() => _stub.Dispose();

    private OfflineProfileWriter CreateWriter()
    {
        _stub.Profile = new ProfileResponse(_account, "Alice", "alice@example.test", "de-CH", true);
        var http = new HttpClient(_stub) { BaseAddress = new Uri("https://api.test/") };
        var api = new AccountApiClient(http, http);
        return new OfflineProfileWriter(api, _local, _culture, new StubAuthStateProvider(_account));
    }

    [Fact]
    public async Task Save_WhenOnline_PutsToTheServer_AndQueuesNothing()
    {
        var writer = CreateWriter();

        var result = await writer.SaveAsync(new UpdateProfileRequest("Alice", "fr-CH"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _stub.PutCount);
        var pending = await _local.LoadProfileAsync(default);
        Assert.False(pending!.Dirty);
    }

    [Fact]
    public async Task Save_WhenOffline_QueuesTheEditAndReportsSuccess()
    {
        var writer = CreateWriter();
        _stub.Offline = true;

        var result = await writer.SaveAsync(new UpdateProfileRequest("Alice", "fr-CH"));

        // The page must be able to apply the language and confirm even though the server was unreachable.
        Assert.True(result.IsSuccess);
        Assert.Equal("fr-CH", result.Value!.Language);
        Assert.Equal(0, _stub.PutCount);

        var pending = await _local.LoadProfileAsync(default);
        Assert.NotNull(pending);
        Assert.True(pending!.Dirty);
        Assert.Equal("fr-CH", pending.Language);
        Assert.Equal(_account, pending.AccountScope);
    }

    [Fact]
    public async Task Load_WhenOffline_ReturnsTheQueuedEdit_NotAnError()
    {
        var writer = CreateWriter();
        _stub.Offline = true;
        await writer.SaveAsync(new UpdateProfileRequest("Alice", "fr-CH"));

        var loaded = await writer.LoadAsync();

        Assert.True(loaded.IsSuccess);
        Assert.Equal("fr-CH", loaded.Value!.Language);
    }

    [Fact]
    public async Task Reconcile_OnReconnect_FlushesTheQueuedEdit_AndClearsPending()
    {
        var writer = CreateWriter();
        _stub.Offline = true;
        await writer.SaveAsync(new UpdateProfileRequest("Alice", "fr-CH"));

        // Back online: the next sync trigger flushes the queued edit to the authoritative endpoint.
        _stub.Offline = false;
        await writer.ReconcileAsync(_account);

        Assert.Equal(1, _stub.PutCount);
        Assert.Equal("fr-CH", _stub.LastPut!.Language);
        var pending = await _local.LoadProfileAsync(default);
        Assert.False(pending!.Dirty);
    }

    [Fact]
    public async Task Reconcile_WithNoPendingEdit_AdoptsAnotherDevicesLanguage()
    {
        var writer = CreateWriter();
        await writer.ReconcileAsync(_account); // this device now knows the account language

        // Another device changed the account language to fr-CH; there is no local edit pending here.
        _stub.Profile = _stub.Profile with { Language = "fr-CH" };
        await writer.ReconcileAsync(_account);

        Assert.Equal("fr-CH", _culture.LastSet);
        Assert.Equal(0, _stub.PutCount);
    }

    [Fact]
    public async Task Reconcile_KeepsALanguageChosenForThisDevice_WhileTheAccountLanguageIsUnchanged()
    {
        // Settings says the language picked there applies "only on this device" (2026-10-16 test): a sync that
        // finds the same account language as before must not switch it back.
        var writer = CreateWriter();
        _stub.Profile = _stub.Profile with { Language = "fr-CH" };
        await writer.ReconcileAsync(_account);
        await writer.ReconcileAsync(_account);

        Assert.Null(_culture.LastSet);
    }

    [Fact]
    public async Task Reconcile_WhenLanguageIsUnchanged_DoesNotSwitchCulture()
    {
        var writer = CreateWriter();

        await writer.ReconcileAsync(_account);

        Assert.Null(_culture.LastSet);
    }
}
