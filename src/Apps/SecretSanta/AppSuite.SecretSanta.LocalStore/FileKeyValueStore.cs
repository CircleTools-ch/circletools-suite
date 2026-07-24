namespace AppSuite.SecretSanta.LocalStore;

/// <summary>Options for <see cref="FileKeyValueStore"/>.</summary>
/// <param name="Directory">The directory under which key files are stored (e.g. the app data directory).</param>
public sealed record FileKeyValueStoreOptions(string Directory);

/// <summary>
/// <see cref="IKeyValueStore"/> that stores each key as a JSON file inside a configured directory.
/// Used by the MAUI host, which supplies <c>FileSystem.AppDataDirectory</c>.
/// </summary>
/// <param name="options">The options carrying the target directory.</param>
public sealed class FileKeyValueStore(FileKeyValueStoreOptions options) : IKeyValueStore
{
    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Directory);
        await File.WriteAllTextAsync(PathFor(key), value, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var safe = string.Join('_', key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(options.Directory, $"{safe}.json");
    }
}
