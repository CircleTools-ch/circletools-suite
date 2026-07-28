using System.Collections.Concurrent;
using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>Options for <see cref="FileKeyValueStore"/>.</summary>
/// <param name="Directory">The directory under which key files are stored (e.g. the app data directory).</param>
public sealed record FileKeyValueStoreOptions(string Directory);

/// <summary>
/// <see cref="IKeyValueStore"/> for hosts with a file system rather than web storage — the MAUI
/// head, which supplies <c>FileSystem.AppDataDirectory</c>.
/// </summary>
/// <remarks>
/// <see cref="StorageScope.Durable"/> is a JSON file per key. <see cref="StorageScope.Session"/> is
/// an in-memory dictionary: a native app has no equivalent of <c>sessionStorage</c>, and the
/// closest honest match for "survives a reload but not the session" is the process lifetime.
/// </remarks>
/// <param name="options">The options carrying the target directory.</param>
public sealed class FileKeyValueStore(FileKeyValueStoreOptions options) : IKeyValueStore
{
    private readonly ConcurrentDictionary<string, string> _session = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(
        StorageScope scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (scope == StorageScope.Session)
        {
            return _session.TryGetValue(key, out var value) ? value : null;
        }

        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (IOException)
        {
            // Unreadable is indistinguishable from absent to every caller, and the contract says a
            // read never throws.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> SetAsync(
        StorageScope scope,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (scope == StorageScope.Session)
        {
            _session[key] = value;
            return true;
        }

        try
        {
            Directory.CreateDirectory(options.Directory);
            await File.WriteAllTextAsync(PathFor(key), value, cancellationToken);
            return true;
        }
        catch (IOException)
        {
            // A full disk or a locked file. Reported, not swallowed: the caller decides whether
            // losing this write matters (see IKeyValueStore).
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(StorageScope scope, string key, CancellationToken cancellationToken = default)
    {
        if (scope == StorageScope.Session)
        {
            _session.TryRemove(key, out _);
            return ValueTask.CompletedTask;
        }

        try
        {
            var path = PathFor(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Nothing to do about it, and the contract says a removal never throws.
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }

    private string PathFor(string key)
    {
        var safe = string.Join('_', key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(options.Directory, $"{safe}.json");
    }
}
