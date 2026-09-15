using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Stores which device-local reminder kinds the user has switched on (MNF-8, FR-046). Every kind is
/// <strong>off by default</strong> (opt-in). Like the other Secret-Santa device preferences this lives in
/// the plain local <see cref="IKeyValueStore"/> — it is a per-device choice, never synced. Best-effort:
/// a storage failure reads as "nothing enabled" rather than an error.
/// </summary>
/// <param name="store">The device-local key/value store.</param>
public sealed class NotificationPreferences(IKeyValueStore store)
{
    private const string Key = "secretsanta.notifications";

    /// <summary>The reminder kinds currently switched on (empty by default).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The enabled kinds.</returns>
    public async ValueTask<IReadOnlySet<ReminderKind>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, Key, cancellationToken);
            if (string.IsNullOrEmpty(raw))
            {
                return new HashSet<ReminderKind>();
            }

            var enabled = new HashSet<ReminderKind>();
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ReminderKind>(token, out var kind))
                {
                    enabled.Add(kind);
                }
            }

            return enabled;
        }
        catch
        {
            return new HashSet<ReminderKind>();
        }
    }

    /// <summary>Whether a specific reminder kind is switched on.</summary>
    /// <param name="kind">The reminder kind.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when it is enabled.</returns>
    public async ValueTask<bool> IsEnabledAsync(ReminderKind kind, CancellationToken cancellationToken = default) =>
        (await GetEnabledAsync(cancellationToken)).Contains(kind);

    /// <summary>Switches one reminder kind on or off.</summary>
    /// <param name="kind">The reminder kind.</param>
    /// <param name="enabled"><see langword="true"/> to switch on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is stored (or silently skipped).</returns>
    public async ValueTask SetEnabledAsync(ReminderKind kind, bool enabled, CancellationToken cancellationToken = default)
    {
        var current = new HashSet<ReminderKind>(await GetEnabledAsync(cancellationToken));
        if (enabled)
        {
            current.Add(kind);
        }
        else
        {
            current.Remove(kind);
        }

        try
        {
            await store.SetAsync(StorageScope.Durable, Key, string.Join(',', current), cancellationToken);
        }
        catch
        {
            // Best-effort: a failed write just means the toggle is not remembered next launch.
        }
    }
}
