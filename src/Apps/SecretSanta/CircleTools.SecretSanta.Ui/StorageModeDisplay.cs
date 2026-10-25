using CircleTools.SecretSanta.LocalStore.Online;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>How a group shows in lists and headers (ADR-0086): kept on this device, online, or one you take part in.</summary>
public enum StorageKind
{
    /// <summary>Only on this device.</summary>
    Device,

    /// <summary>Online, and this device organizes it.</summary>
    Online,

    /// <summary>Online, and this device takes part in it.</summary>
    TakingPart,
}

/// <summary>
/// The one place that maps a group's place to its icon, label and explanation (ADR-0086), so the create dialog,
/// the settings default, the list chip and the group header always agree.
/// </summary>
public static class StorageModeDisplay
{
    /// <summary>The places a new group can be kept, in display order.</summary>
    public static IReadOnlyList<GroupPlace> Choices { get; } = [GroupPlace.Device, GroupPlace.Online];

    /// <summary>The icon for a place.</summary>
    /// <param name="place">The place.</param>
    /// <returns>A Material icon.</returns>
    public static string Icon(GroupPlace place) => place == GroupPlace.Online ? Icon(StorageKind.Online) : Icon(StorageKind.Device);

    /// <summary>The resource key of a place's label.</summary>
    /// <param name="place">The place.</param>
    /// <returns>The key.</returns>
    public static string LabelKey(GroupPlace place) => place == GroupPlace.Online ? "PlaceOnlineTitle" : "ModeLocalTitle";

    /// <summary>
    /// The resource key of a place's one-line explanation. Online means "in your account" while signed in and
    /// "organized by a link this device keeps" while signed out (ADR-0086, decision D1).
    /// </summary>
    /// <param name="place">The place.</param>
    /// <param name="signedIn">Whether someone is signed in.</param>
    /// <returns>The key.</returns>
    public static string HintKey(GroupPlace place, bool signedIn) => place switch
    {
        GroupPlace.Online => signedIn ? "PlaceOnlineHint" : "PlaceOnlineSignedOutHint",
        _ => "ModeLocalHint",
    };

    /// <summary>How a group this device holds shows.</summary>
    /// <param name="record">The group record.</param>
    /// <returns>Its kind.</returns>
    public static StorageKind KindOf(GroupRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record switch
        {
            { Place: GroupPlace.Device } => StorageKind.Device,
            { Document: null } => StorageKind.TakingPart,
            _ => StorageKind.Online,
        };
    }

    /// <summary>The icon for a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A Material icon.</returns>
    public static string Icon(StorageKind kind) => kind switch
    {
        StorageKind.Online => Icons.Material.Filled.Cloud,
        StorageKind.TakingPart => Icons.Material.Filled.CardGiftcard,
        _ => Icons.Material.Filled.Smartphone,
    };

    /// <summary>The resource key of a kind's label.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The key.</returns>
    public static string LabelKey(StorageKind kind) => kind switch
    {
        StorageKind.Online => "PlaceOnlineTitle",
        StorageKind.TakingPart => "ChipTakingPart",
        _ => "ModeLocalTitle",
    };
}
