using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Stores the device-local <strong>default place</strong> for new groups (FR-048, ADR-0071, ADR-0086) and
/// whether the one-time storage onboarding has been shown. Like the other Secret-Santa device
/// preferences (reminders, recency) this lives in the plain local <see cref="IKeyValueStore"/> — a
/// per-device choice, never synced: a signed-out user needs a default too, and the groups themselves
/// keep their own place. Best-effort: a read/write failure reads as
/// "default = On this device" rather than surfacing an error.
/// </summary>
/// <param name="store">The device-local key/value store.</param>
public sealed class StorageModePreferences(IKeyValueStore store)
{
    private const string DefaultModeKey = "secretsanta.storage.default";
    private const string OnboardedKey = "secretsanta.storage.onboarded";

    /// <summary>The default place for new groups — <see cref="GroupPlace.Device"/> until the user changes it, so
    /// local-first (NFR-001) holds for anyone who does nothing. A default stored by the older three-mode picker
    /// ("SharedByLink", "Account") reads as online.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The remembered default, or <see cref="GroupPlace.Device"/>.</returns>
    public async ValueTask<GroupPlace> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, DefaultModeKey, cancellationToken);
            return raw is "Online" or "SharedByLink" or "Account" ? GroupPlace.Online : GroupPlace.Device;
        }
        catch
        {
            return GroupPlace.Device;
        }
    }

    /// <summary>Remembers the default place for new groups.</summary>
    /// <param name="place">The place to make the default.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is stored (or silently skipped).</returns>
    public async ValueTask SetDefaultAsync(GroupPlace place, CancellationToken cancellationToken = default)
    {
        try
        {
            await store.SetAsync(StorageScope.Durable, DefaultModeKey, place.ToString(), cancellationToken);
        }
        catch
        {
            // Best-effort: a failed write just means the default is not remembered next launch.
        }
    }

    /// <summary>Whether the one-time storage onboarding has already been shown.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when it has been shown (or when the store cannot be read).</returns>
    public async ValueTask<bool> IsOnboardedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await store.GetAsync(StorageScope.Durable, OnboardedKey, cancellationToken) == "1";
        }
        catch
        {
            // On a read failure, treat as onboarded so a broken store never traps the user in a dialog loop.
            return true;
        }
    }

    /// <summary>Marks the one-time storage onboarding as shown.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the flag is stored (or silently skipped).</returns>
    public async ValueTask MarkOnboardedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await store.SetAsync(StorageScope.Durable, OnboardedKey, "1", cancellationToken);
        }
        catch
        {
            // Best-effort: if this fails the dialog may show again next launch; harmless.
        }
    }
}
