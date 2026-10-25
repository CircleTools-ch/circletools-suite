using System.Text.Json;
using System.Text.Json.Serialization;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// Exports and re-imports the whole set of locally held groups as a single portable JSON file.
/// </summary>
/// <remarks>
/// The web head keeps a group only in browser storage (<see cref="Online.GroupRecordStore"/>), which the
/// browser can evict under storage pressure or a user can clear with one "clear browsing data" click.
/// A downloadable backup is the local-first answer to "where is my data and how do I keep it": it makes
/// the data tangible, portable to another browser or device, and recoverable after a wipe — without a
/// server or an account (NFR-001). The backup reuses the same <see cref="GroupState"/> memento the store
/// already persists, so a file made by one version reads back through the ordinary load-time migrations.
/// </remarks>
/// <param name="groups">The group repository the host has wired.</param>
public sealed class BackupService(IGroupRepository groups)
{
    /// <summary>The <c>format</c> marker written into every backup, so an unrelated file is rejected.</summary>
    public const string Format = "circletools.secretsanta.backup";

    /// <summary>The current backup schema version. Bumped only if the envelope shape changes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Serializes every group held on this device to a self-describing JSON backup document.
    /// </summary>
    public async Task<string> ExportAsync(CancellationToken cancellationToken)
    {
        var all = await groups.ListAsync(cancellationToken);
        var states = all.Select(g => g.ToState()).ToList();
        var document = new BackupDocument(Format, CurrentVersion, DateTimeOffset.UtcNow, states);
        return JsonSerializer.Serialize(document, BackupJsonContext.Default.BackupDocument);
    }

    /// <summary>
    /// Restores groups from a backup produced by <see cref="ExportAsync"/>. Groups are merged by id:
    /// a group whose id is new is added; a group whose id already exists is overwritten with the backed-up
    /// copy. Nothing already on the device is deleted, so an import can only add or refresh — never wipe.
    /// </summary>
    public async Task<BackupImportResult> ImportAsync(string json, CancellationToken cancellationToken)
    {
        BackupDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, BackupJsonContext.Default.BackupDocument);
        }
        catch (JsonException)
        {
            return BackupImportResult.Invalid;
        }

        if (document is null || !string.Equals(document.Format, Format, StringComparison.Ordinal) || document.Groups is null)
        {
            return BackupImportResult.Invalid;
        }

        if (document.Version > CurrentVersion)
        {
            return BackupImportResult.UnsupportedVersion;
        }

        var existing = (await groups.ListAsync(cancellationToken)).Select(g => g.Id).ToHashSet();
        var added = 0;
        var updated = 0;
        var skipped = 0;
        foreach (var state in document.Groups)
        {
            Group group;
            try
            {
                group = Group.FromState(state);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // A single malformed entry is skipped rather than failing the whole restore, so a
                // partially corrupt file still recovers everything it can.
                skipped++;
                continue;
            }

            await groups.SaveAsync(group, cancellationToken);
            if (existing.Contains(state.Id))
            {
                updated++;
            }
            else
            {
                added++;
            }
        }

        return BackupImportResult.Restored(added, updated, skipped);
    }
}

/// <summary>The self-describing envelope written to a backup file.</summary>
/// <param name="Format">Fixed marker identifying the file as a Secret-Santa backup.</param>
/// <param name="Version">The backup schema version (see <see cref="BackupService.CurrentVersion"/>).</param>
/// <param name="ExportedAt">When the backup was produced (UTC), for the user's reference.</param>
/// <param name="Groups">The backed-up group mementos.</param>
public sealed record BackupDocument(
    string Format,
    int Version,
    DateTimeOffset ExportedAt,
    IReadOnlyList<GroupState> Groups);

/// <summary>The outcome of a <see cref="BackupService.ImportAsync"/> call.</summary>
public enum BackupImportOutcome
{
    /// <summary>The backup was read and its groups were merged in.</summary>
    Restored,

    /// <summary>The file was not a readable Secret-Santa backup.</summary>
    Invalid,

    /// <summary>The backup was written by a newer app version than this one can read.</summary>
    UnsupportedVersion,
}

/// <summary>The result of restoring a backup, including how many groups changed.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Added">Groups newly added to this device.</param>
/// <param name="Updated">Existing groups overwritten by the backed-up copy.</param>
/// <param name="Skipped">Entries that could not be read and were left out.</param>
public sealed record BackupImportResult(BackupImportOutcome Outcome, int Added, int Updated, int Skipped)
{
    /// <summary>The file was not a valid backup.</summary>
    public static readonly BackupImportResult Invalid = new(BackupImportOutcome.Invalid, 0, 0, 0);

    /// <summary>The backup is from a newer app version.</summary>
    public static readonly BackupImportResult UnsupportedVersion = new(BackupImportOutcome.UnsupportedVersion, 0, 0, 0);

    /// <summary>A successful restore with the counts of what changed.</summary>
    public static BackupImportResult Restored(int added, int updated, int skipped) =>
        new(BackupImportOutcome.Restored, added, updated, skipped);
}

/// <summary>Source-generated JSON context for the portable backup document.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(BackupDocument))]
public sealed partial class BackupJsonContext : JsonSerializerContext;
