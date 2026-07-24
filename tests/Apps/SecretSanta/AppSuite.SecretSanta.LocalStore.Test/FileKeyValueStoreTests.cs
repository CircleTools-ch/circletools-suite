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

        await store.SetAsync("groups", "payload", default);

        Assert.Equal("payload", await store.GetAsync("groups", default));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        Assert.Null(await CreateStore().GetAsync("absent", default));
    }

    [Fact]
    public async Task Set_ThenReadFromNewInstance_PersistsToDisk()
    {
        await CreateStore().SetAsync("groups", "durable", default);

        // A brand-new store pointing at the same directory must see the value.
        Assert.Equal("durable", await CreateStore().GetAsync("groups", default));
    }

    [Fact]
    public async Task Remove_DeletesValue()
    {
        var store = CreateStore();
        await store.SetAsync("groups", "payload", default);

        await store.RemoveAsync("groups", default);

        Assert.Null(await store.GetAsync("groups", default));
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
