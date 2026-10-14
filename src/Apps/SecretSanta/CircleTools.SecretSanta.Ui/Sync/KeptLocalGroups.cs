using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The account-free groups the user has already decided to keep on this device, so the sign-in
/// reconciliation (ADR-0044) does not ask about them again on every sign-in.
/// </summary>
/// <remarks>
/// A group lands here when the user answers the sign-in prompt with "Cancel" (keep local), or creates a
/// group as "On this device" while signed in — both are explicit choices, and re-asking each sign-in
/// "you created these while signed out" was both repetitive and, for the second case, untrue (2026-10-14
/// review). Device-local and best-effort, like the other UI preferences: a storage failure just means
/// the prompt may ask once more. Moving a group into the account later is still one tap in Sharing &amp; sync.
/// </remarks>
/// <param name="store">The device-local key-value store.</param>
public sealed class KeptLocalGroups(IKeyValueStore store)
{
    private const string Key = "secretsanta.reconcile.keptLocal";

    /// <summary>Reads the ids of the groups the user chose to keep local.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The kept group ids; empty when none or unreadable.</returns>
    public async Task<HashSet<Guid>> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, Key, cancellationToken);
            return raw is null
                ? []
                : [.. raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty)];
        }
        catch (StorageUnavailableException)
        {
            return [];
        }
    }

    /// <summary>Records groups the user chose to keep on this device.</summary>
    /// <param name="ids">The group ids to remember.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is stored (or could not be).</returns>
    public async Task AddAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var kept = await GetAsync(cancellationToken);
        kept.UnionWith(ids);
        try
        {
            await store.SetAsync(StorageScope.Durable, Key, string.Join('\n', kept), cancellationToken);
        }
        catch (StorageUnavailableException)
        {
            // Best-effort: the prompt may ask about these once more.
        }
    }
}
