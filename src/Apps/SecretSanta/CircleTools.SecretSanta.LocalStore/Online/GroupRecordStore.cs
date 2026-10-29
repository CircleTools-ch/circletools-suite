using System.Text.Json;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>This device's group records (ADR-0086), read and written as one document.</summary>
public interface IGroupRecordStore
{
    /// <summary>Loads the document; empty when nothing is stored yet.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The document.</returns>
    Task<GroupRecordsDocument> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(GroupRecordsDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IGroupRecordStore"/> over the durable key-value store, under its own key. The first load on a device
/// that used the old three-tier model imports the groups it organized there as groups on this device: the server
/// side starts empty (ADR-0086, decision D2), but what a device holds is never lost (NFR-001).
/// </summary>
/// <param name="store">The key-value store.</param>
public sealed class GroupRecordStore(IKeyValueStore store) : IGroupRecordStore
{
    /// <summary>The storage key.</summary>
    public const string StorageKey = "circletools.secretsanta.groups.v4";

    /// <summary>Where a stored document this version can't read is set aside before anything is written (ADR-0087).</summary>
    public const string UnreadableStorageKey = StorageKey + ".unreadable";

    /// <summary>The key the old model kept its groups under (a bare array, or a wrapper with sync metadata).</summary>
    public const string LegacyStorageKey = "circletools.secretsanta.groups";

    /// <inheritdoc />
    public async Task<GroupRecordsDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await store.GetAsync(StorageScope.Durable, StorageKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return await ImportLegacyAsync(cancellationToken);
        }

        GroupRecordsDocument? document = null;
        try
        {
            document = JsonSerializer.Deserialize(json, OnlineJsonContext.Default.GroupRecordsDocument);
        }
        catch (JsonException)
        {
            // Handled below with any other document this version can't read.
        }

        if (document is { SchemaVersion: GroupRecordsDocument.CurrentSchemaVersion })
        {
            return document with { Removals = document.Removals ?? [], Invitations = document.Invitations ?? [], Parked = document.Parked ?? [] };
        }

        // Damaged, or written by another version: the next save would overwrite it, and groups kept on this device
        // exist nowhere else. Keep the text under its own key first, so it can be recovered (ADR-0087).
        await PreserveUnreadableAsync(json, cancellationToken);
        return GroupRecordsDocument.Empty;
    }

    // The first unreadable document is kept and never overwritten: it is the one most likely to hold the groups.
    private async Task PreserveUnreadableAsync(string json, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(await store.GetAsync(StorageScope.Durable, UnreadableStorageKey, cancellationToken))
            && !await store.SetAsync(StorageScope.Durable, UnreadableStorageKey, json, cancellationToken))
        {
            throw new StorageUnavailableException("The groups could not be read, and could not be set aside: local storage is unavailable or full.");
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(GroupRecordsDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var json = JsonSerializer.Serialize(document, OnlineJsonContext.Default.GroupRecordsDocument);
        if (!await store.SetAsync(StorageScope.Durable, StorageKey, json, cancellationToken))
        {
            throw new StorageUnavailableException("The groups could not be saved: local storage is unavailable or full.");
        }
    }

    // Once per device: the old model's own organizing copies become groups on this device. A group joined as a
    // participant, or held through a join link only, is not this device's to organize, so it is left behind; the
    // person joins again. The old key goes only after the import is saved.
    private async Task<GroupRecordsDocument> ImportLegacyAsync(CancellationToken cancellationToken)
    {
        var legacy = await store.GetAsync(StorageScope.Durable, LegacyStorageKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(legacy))
        {
            return GroupRecordsDocument.Empty;
        }

        var records = new List<GroupRecord>();
        try
        {
            using var parsed = JsonDocument.Parse(legacy);
            var root = parsed.RootElement;
            var states = root.ValueKind == JsonValueKind.Array
                ? root.EnumerateArray()
                : root.TryGetProperty("records", out var items) && items.ValueKind == JsonValueKind.Array
                    ? items.EnumerateArray().Where(IsOwnOrganizingCopy).Select(r => r.GetProperty("state"))
                    : [];
            foreach (var state in states)
            {
                if (GroupDocument.TryDeserialize(state.GetRawText(), out var document) && records.All(r => r.Id != document!.Id))
                {
                    records.Add(new GroupRecord(document!.Id, GroupPlace.Device, document, null, 0, false));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return GroupRecordsDocument.Empty;
        }

        var imported = GroupRecordsDocument.Empty with { Records = records };
        await SaveAsync(imported, cancellationToken);
        await store.RemoveAsync(StorageScope.Durable, LegacyStorageKey, cancellationToken);
        return imported;
    }

    private static bool IsOwnOrganizingCopy(JsonElement record) =>
        record.TryGetProperty("state", out _)
        && !HasValue(record, "claimedMemberId")
        && !HasValue(record, "joinedAccount")
        && !(IsSharedByLink(record) && !(record.TryGetProperty("tokens", out var tokens) && HasValue(tokens, "adminToken")));

    private static bool IsSharedByLink(JsonElement record) =>
        record.TryGetProperty("mode", out var mode)
        && ((mode.ValueKind == JsonValueKind.Number && mode.GetInt32() == 1)
            || (mode.ValueKind == JsonValueKind.String && mode.GetString() == "SharedByLink"));

    private static bool HasValue(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
}
