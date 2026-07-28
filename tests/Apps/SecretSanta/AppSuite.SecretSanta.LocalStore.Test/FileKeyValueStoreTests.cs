using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore.Test;

/// <summary>
/// Tests for <see cref="FileKeyValueStore"/> (the MAUI persistence adapter), exercised against a
/// unique temporary directory that is removed after each test.
/// </summary>
public sealed class FileKeyValueStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "appsuite-secretsanta-tests", Guid.NewGuid().ToString("N"));

    private FileKeyValueStore CreateStore() => new(new FileKeyValueStoreOptions(_directory));

    [Fact]
    public async Task SetThenGet_ReturnsStoredValue()
    {
        var store = CreateStore();

        Assert.True(await store.SetAsync(StorageScope.Durable, "groups", "payload", default));

        Assert.Equal("payload", await store.GetAsync(StorageScope.Durable, "groups", default));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        Assert.Null(await CreateStore().GetAsync(StorageScope.Durable, "absent", default));
    }

    [Fact]
    public async Task Set_ThenReadFromNewInstance_PersistsToDisk()
    {
        await CreateStore().SetAsync(StorageScope.Durable, "groups", "durable", default);

        // A brand-new store pointing at the same directory must see the value.
        Assert.Equal("durable", await CreateStore().GetAsync(StorageScope.Durable, "groups", default));
    }

    [Fact]
    public async Task Remove_DeletesValue()
    {
        var store = CreateStore();
        await store.SetAsync(StorageScope.Durable, "groups", "payload", default);

        await store.RemoveAsync(StorageScope.Durable, "groups", default);

        Assert.Null(await store.GetAsync(StorageScope.Durable, "groups", default));
    }

    /// <summary>
    /// Session scope is the process lifetime on a device: readable within one store instance, and
    /// never written to disk, so a second instance sees nothing.
    /// </summary>
    [Fact]
    public async Task SessionScope_IsInMemoryAndNotPersisted()
    {
        var store = CreateStore();

        Assert.True(await store.SetAsync(StorageScope.Session, "token", "secret", default));
        Assert.Equal("secret", await store.GetAsync(StorageScope.Session, "token", default));

        Assert.Null(await CreateStore().GetAsync(StorageScope.Session, "token", default));
    }

    /// <summary>The two scopes are separate namespaces, not two names for the same slot.</summary>
    [Fact]
    public async Task Scopes_DoNotSeeEachOthersValues()
    {
        var store = CreateStore();

        await store.SetAsync(StorageScope.Durable, "key", "on-disk", default);
        await store.SetAsync(StorageScope.Session, "key", "in-memory", default);

        Assert.Equal("on-disk", await store.GetAsync(StorageScope.Durable, "key", default));
        Assert.Equal("in-memory", await store.GetAsync(StorageScope.Session, "key", default));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
