using CircleTools.SecretSanta.LocalStore;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Shared icon + label mapping for a group's storage mode (FR-038, ADR-0073), so the list card, the
/// create field and anywhere else that shows "where this group lives" read identically.
/// </summary>
public static class StorageModeDisplay
{
    /// <summary>The Material icon for a storage mode: device / link / cloud.</summary>
    /// <param name="mode">The storage mode.</param>
    /// <returns>A MudBlazor icon path.</returns>
    public static string Icon(StorageMode mode) => mode switch
    {
        StorageMode.SharedByLink => Icons.Material.Filled.Link,
        StorageMode.Account => Icons.Material.Filled.CloudDone,
        _ => Icons.Material.Filled.Smartphone,
    };

    /// <summary>The <c>AppStrings</c> resource key for the mode's short title (localised by the caller).</summary>
    /// <param name="mode">The storage mode.</param>
    /// <returns>A resource key: <c>ModeLocalTitle</c> / <c>ModeSharedTitle</c> / <c>ModeAccountTitle</c>.</returns>
    public static string LabelKey(StorageMode mode) => mode switch
    {
        StorageMode.SharedByLink => "ModeSharedTitle",
        StorageMode.Account => "ModeAccountTitle",
        _ => "ModeLocalTitle",
    };
}
