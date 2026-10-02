using System.Text.Json;
using System.Text.Json.Serialization;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Source-generated JSON for the recency lists (ADR-0072): reflection-based serialization of
/// <c>List&lt;Guid&gt;</c> throws on the fully-trimmed/AOT iOS head, which would break opening or creating a
/// group (the recency is recorded on navigation). Source generation keeps it AOT-safe regardless of the
/// <c>JsonSerializerIsReflectionEnabledByDefault</c> switch.
/// </summary>
[JsonSerializable(typeof(List<Guid>))]
internal sealed partial class RecentAccessJsonContext : JsonSerializerContext;

/// <summary>
/// Tracks the most-recently-opened groups and events for the navigation's quick-access section (R2-5).
/// Recency is a per-device convenience — where you last were on <em>this</em> device — so it lives in
/// the plain local <see cref="IKeyValueStore"/> (like the culture choice) rather than the synced,
/// account-scoped preferences document that pinned groups use. Every read and write is best-effort: a
/// storage failure or a malformed value yields an empty list rather than an error, because a missing
/// shortcut must never break navigation.
/// </summary>
/// <param name="store">The device-local key/value store.</param>
public sealed class RecentAccessService(IKeyValueStore store)
{
    private const string GroupsKey = "secretsanta.recentGroups";
    private const string EventsKey = "secretsanta.recentEvents";
    private const int Cap = 12;

    /// <summary>Records that a group was opened, moving it to the front of the recent list.</summary>
    /// <param name="groupId">The group just opened.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the recency has been recorded (or silently skipped).</returns>
    public ValueTask RecordGroupAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        PushAsync(GroupsKey, groupId, cancellationToken);

    /// <summary>Records that an event was opened, moving it to the front of the recent list.</summary>
    /// <param name="eventId">The event just opened.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the recency has been recorded (or silently skipped).</returns>
    public ValueTask RecordEventAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        PushAsync(EventsKey, eventId, cancellationToken);

    /// <summary>The recently-opened group ids, most recent first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recent group ids, or an empty list.</returns>
    public ValueTask<IReadOnlyList<Guid>> GetRecentGroupsAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(GroupsKey, cancellationToken);

    /// <summary>The recently-opened event ids, most recent first (used to order undated occasions).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recent event ids, or an empty list.</returns>
    public ValueTask<IReadOnlyList<Guid>> GetRecentEventsAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(EventsKey, cancellationToken);

    private async ValueTask PushAsync(string key, Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var list = new List<Guid>(await ReadAsync(key, cancellationToken));
            list.Remove(id);
            list.Insert(0, id);
            if (list.Count > Cap)
            {
                list.RemoveRange(Cap, list.Count - Cap);
            }

            await store.SetAsync(StorageScope.Durable, key, JsonSerializer.Serialize(list, RecentAccessJsonContext.Default.ListGuid), cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // Recency is a convenience; a lost write just means the shortcut does not update this time.
        }
    }

    private async ValueTask<IReadOnlyList<Guid>> ReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, key, cancellationToken);
            if (string.IsNullOrEmpty(raw))
            {
                return [];
            }

            return JsonSerializer.Deserialize(raw, RecentAccessJsonContext.Default.ListGuid) ?? [];
        }
        catch (StorageUnavailableException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
