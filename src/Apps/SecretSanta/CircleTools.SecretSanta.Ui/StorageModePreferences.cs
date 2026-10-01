using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Stores the device-local <strong>default storage mode</strong> for new groups (FR-048, ADR-0071) and
/// whether the one-time storage onboarding has been shown. Like the other Secret-Santa device
/// preferences (reminders, recency) this lives in the plain local <see cref="IKeyValueStore"/> — a
/// per-device choice, never synced: a signed-out user needs a default too, and the groups themselves
/// still sync by their own <see cref="StorageMode"/>. Best-effort: a read/write failure reads as
/// "default = On this device" rather than surfacing an error.
/// </summary>
/// <param name="store">The device-local key/value store.</param>
public sealed class StorageModePreferences(IKeyValueStore store)
{
    private const string DefaultModeKey = "secretsanta.storage.default";
    private const string OnboardedKey = "secretsanta.storage.onboarded";

    /// <summary>The default storage mode for new groups — <see cref="StorageMode.OnThisDevice"/> until
    /// the user changes it, so local-first (NFR-001) holds for anyone who does nothing.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The remembered default, or <see cref="StorageMode.OnThisDevice"/>.</returns>
    public async ValueTask<StorageMode> GetDefaultAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, DefaultModeKey, cancellationToken);
            return Enum.TryParse<StorageMode>(raw, out var mode) && Enum.IsDefined(mode)
                ? mode
                : StorageMode.OnThisDevice;
        }
        catch
        {
            return StorageMode.OnThisDevice;
        }
    }

    /// <summary>Remembers the default storage mode for new groups.</summary>
    /// <param name="mode">The mode to make the default.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the choice is stored (or silently skipped).</returns>
    public async ValueTask SetDefaultAsync(StorageMode mode, CancellationToken cancellationToken = default)
    {
        try
        {
            await store.SetAsync(StorageScope.Durable, DefaultModeKey, mode.ToString(), cancellationToken);
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
