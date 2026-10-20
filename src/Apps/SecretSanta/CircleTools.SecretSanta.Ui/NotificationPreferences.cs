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

    // Kinds the user asked for while notifications were blocked (Q8, R15): applied once they are allowed.
    private const string WantedKey = "secretsanta.notifications.wanted";

    /// <summary>The reminder kinds currently switched on (empty by default).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The enabled kinds.</returns>
    public ValueTask<IReadOnlySet<ReminderKind>> GetEnabledAsync(CancellationToken cancellationToken = default) =>
        ReadKindsAsync(Key, cancellationToken);

    /// <summary>
    /// The reminder kinds the user asked for while notifications were blocked — kept on this device so the wish
    /// survives leaving the page (or the app) for the system settings (R15, 2026-10-20 device test).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The wanted kinds.</returns>
    public ValueTask<IReadOnlySet<ReminderKind>> GetWantedAsync(CancellationToken cancellationToken = default) =>
        ReadKindsAsync(WantedKey, cancellationToken);

    /// <summary>Remembers that the user asked for a reminder kind while notifications were blocked.</summary>
    /// <param name="kind">The kind asked for.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the wish is stored (or silently skipped).</returns>
    public async ValueTask AddWantedAsync(ReminderKind kind, CancellationToken cancellationToken = default)
    {
        var wanted = new HashSet<ReminderKind>(await GetWantedAsync(cancellationToken)) { kind };
        await WriteKindsAsync(WantedKey, wanted, cancellationToken);
    }

    /// <summary>
    /// Switches on every kind the user asked for while notifications were blocked, and forgets the wishes. Call
    /// only once notifications are allowed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The kinds that were switched on (empty when there was nothing to apply).</returns>
    public async ValueTask<IReadOnlySet<ReminderKind>> ApplyWantedAsync(CancellationToken cancellationToken = default)
    {
        var wanted = await GetWantedAsync(cancellationToken);
        if (wanted.Count == 0)
        {
            return wanted;
        }

        var enabled = new HashSet<ReminderKind>(await GetEnabledAsync(cancellationToken));
        enabled.UnionWith(wanted);
        await WriteKindsAsync(Key, enabled, cancellationToken);
        await WriteKindsAsync(WantedKey, [], cancellationToken);
        return wanted;
    }

    private async ValueTask<IReadOnlySet<ReminderKind>> ReadKindsAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, key, cancellationToken);
            if (string.IsNullOrEmpty(raw))
            {
                return new HashSet<ReminderKind>();
            }

            var kinds = new HashSet<ReminderKind>();
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Enum.TryParse<ReminderKind>(token, out var kind))
                {
                    kinds.Add(kind);
                }
            }

            return kinds;
        }
        catch
        {
            return new HashSet<ReminderKind>();
        }
    }

    private async ValueTask WriteKindsAsync(string key, IEnumerable<ReminderKind> kinds, CancellationToken cancellationToken)
    {
        try
        {
            await store.SetAsync(StorageScope.Durable, key, string.Join(',', kinds), cancellationToken);
        }
        catch
        {
            // Best-effort: a failed write just means the choice is not remembered next launch.
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

        await WriteKindsAsync(Key, current, cancellationToken);

        // A decision made now replaces a wish left from a blocked attempt.
        var wanted = await GetWantedAsync(cancellationToken);
        if (wanted.Contains(kind))
        {
            await WriteKindsAsync(WantedKey, wanted.Where(k => k != kind), cancellationToken);
        }
    }

    // Lead times, time of day, per-occasion overrides and the after-draw offers made (FR-046 amendment). A plain
    // line format ("lead:DrawNotDone=3", "time=09:00", "event:<id>=off|<days>", "offered:<id>") keeps the
    // trimmed web build free of reflection-based JSON.
    private const string SettingsKey = "secretsanta.reminders.settings";

    /// <summary>Reads the reminder settings; defaults when unset or unreadable.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The settings.</returns>
    public async ValueTask<ReminderSettings> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        Parse(await ReadSettingsAsync(cancellationToken)).Settings;

    /// <summary>Stores the reminder settings, keeping the after-draw offers already made.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when stored (best-effort).</returns>
    public async ValueTask SetSettingsAsync(ReminderSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var (_, offered) = Parse(await ReadSettingsAsync(cancellationToken));
        await WriteSettingsAsync(settings, offered, cancellationToken);
    }

    /// <summary>Records that the after-draw reminder offer was made for an occasion; returns whether it was new.</summary>
    /// <param name="eventId">The occasion.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see langword="true"/> the first time for this occasion.</returns>
    public async ValueTask<bool> MarkOfferedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var (settings, offered) = Parse(await ReadSettingsAsync(cancellationToken));
        if (!offered.Add(eventId))
        {
            return false;
        }

        await WriteSettingsAsync(settings, offered, cancellationToken);
        return true;
    }

    private async ValueTask<string?> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await store.GetAsync(StorageScope.Durable, SettingsKey, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private async ValueTask WriteSettingsAsync(ReminderSettings settings, HashSet<Guid> offered, CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            $"lead:{ReminderKind.DrawNotDone}={settings.Lead.DrawNotDoneDays}",
            $"lead:{ReminderKind.EventTomorrow}={settings.Lead.EventTomorrowDays}",
            $"lead:{ReminderKind.MyWishlistEmpty}={settings.Lead.MyWishlistEmptyDays}",
            $"time={settings.TimeOfDay.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}",
        };
        lines.AddRange(settings.Overrides.Select(o =>
            $"event:{o.Key:N}={(o.Value.Off ? "off" : o.Value.EventLeadDays?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default")}"));
        lines.AddRange(offered.Select(id => $"offered:{id:N}"));

        try
        {
            await store.SetAsync(StorageScope.Durable, SettingsKey, string.Join('\n', lines), cancellationToken);
        }
        catch
        {
            // Best-effort, like the toggles.
        }
    }

    private static (ReminderSettings Settings, HashSet<Guid> Offered) Parse(string? raw)
    {
        var settings = ReminderSettings.Default;
        var offered = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (settings, offered);
        }

        var lead = settings.Lead;
        var time = settings.TimeOfDay;
        var overrides = new Dictionary<Guid, EventReminderOverride>();
        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            var key = eq < 0 ? line : line[..eq];
            var value = eq < 0 ? string.Empty : line[(eq + 1)..];
            if (key.StartsWith("lead:", StringComparison.Ordinal)
                && Enum.TryParse<ReminderKind>(key[5..], out var kind)
                && int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var days) && days > 0)
            {
                lead = lead.With(kind, days);
            }
            else if (key == "time" && TimeOnly.TryParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t))
            {
                time = t;
            }
            else if (key.StartsWith("event:", StringComparison.Ordinal) && Guid.TryParse(key[6..], out var eventId))
            {
                overrides[eventId] = value == "off"
                    ? new EventReminderOverride(true, null)
                    : new EventReminderOverride(false, int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null);
            }
            else if (key.StartsWith("offered:", StringComparison.Ordinal) && Guid.TryParse(key[8..], out var offeredId))
            {
                offered.Add(offeredId);
            }
        }

        return (new ReminderSettings(lead, time, overrides), offered);
    }
}
