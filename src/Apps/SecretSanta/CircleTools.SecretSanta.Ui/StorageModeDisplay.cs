using CircleTools.SecretSanta.LocalStore;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Where a group lives, as the app shows it (ADR-0081): on this device, shared by link without an account,
/// in my account, or in my account and shared with a join link. Finer than <see cref="StorageMode"/>, which
/// reads an account-owned shared group as plain "My account".
/// </summary>
public enum StorageKind
{
    /// <summary>On this device only.</summary>
    Device,

    /// <summary>Shared by link, with no account behind it (or joined through the join link).</summary>
    Link,

    /// <summary>In the account sync store.</summary>
    Account,

    /// <summary>Owned by the account and shared with a join link (an admin-claimed shared group).</summary>
    AccountShared,
}

/// <summary>
/// Shared icon + label mapping for a group's storage mode (FR-038, ADR-0073, ADR-0081), so the list card, the
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

    /// <summary>
    /// The storage modes a person can choose, in the order shown (ADR-0081). Signed out: on this device, shared
    /// by link, my account (sign-in needed). Signed in: on this device, my account, my account · shared — where
    /// <see cref="StorageMode.SharedByLink"/> stands for the account-owned shared group.
    /// </summary>
    /// <param name="signedIn">Whether a session is live.</param>
    /// <returns>The modes in display order.</returns>
    public static IReadOnlyList<StorageMode> Choices(bool signedIn) => signedIn
        ? [StorageMode.OnThisDevice, StorageMode.Account, StorageMode.SharedByLink]
        : [StorageMode.OnThisDevice, StorageMode.SharedByLink, StorageMode.Account];

    /// <summary>The title key of a choice, as a signed-in or signed-out person sees it (ADR-0081).</summary>
    /// <param name="mode">The choice.</param>
    /// <param name="signedIn">Whether a session is live.</param>
    /// <returns>A resource key.</returns>
    public static string ChoiceLabelKey(StorageMode mode, bool signedIn) =>
        signedIn && mode == StorageMode.SharedByLink ? "ModeAccountSharedTitle" : LabelKey(mode);

    /// <summary>The one-line hint key of a choice, as a signed-in or signed-out person sees it (ADR-0081).</summary>
    /// <param name="mode">The choice.</param>
    /// <param name="signedIn">Whether a session is live.</param>
    /// <returns>A resource key.</returns>
    public static string ChoiceHintKey(StorageMode mode, bool signedIn) => mode switch
    {
        StorageMode.SharedByLink => signedIn ? "ModeAccountSharedHint" : "ModeSharedHint",
        StorageMode.Account => signedIn ? "ModeAccountHint" : "ModeAccountSignInHint",
        _ => "ModeLocalHint",
    };

    /// <summary>The icon of a choice (ADR-0081).</summary>
    /// <param name="mode">The choice.</param>
    /// <param name="signedIn">Whether a session is live.</param>
    /// <returns>A MudBlazor icon path.</returns>
    public static string ChoiceIcon(StorageMode mode, bool signedIn) =>
        signedIn && mode == StorageMode.SharedByLink ? Icon(StorageKind.AccountShared) : Icon(mode);

    /// <summary>Where a group lives, from its sync record (ADR-0081).</summary>
    /// <param name="record">The group's sync record.</param>
    /// <returns>The kind to show.</returns>
    public static StorageKind KindOf(SyncedGroupRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.AccountScope is not null)
        {
            return StorageKind.Account;
        }

        if (record.IsAdminClaimedShared())
        {
            return StorageKind.AccountShared;
        }

        return record.Mode switch
        {
            StorageMode.SharedByLink => StorageKind.Link,
            StorageMode.Account => StorageKind.Account,
            _ => StorageKind.Device,
        };
    }

    /// <summary>Where a group lives, from its described read model (ADR-0081).</summary>
    /// <param name="group">The described group.</param>
    /// <returns>The kind to show.</returns>
    public static StorageKind KindOf(ClaimableGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group switch
        {
            { AccountScope: not null } => StorageKind.Account,
            { Mode: StorageMode.Account, IsAwaitingClaim: false } => StorageKind.AccountShared,
            { Mode: StorageMode.Account } => StorageKind.Account,
            { Mode: StorageMode.SharedByLink } => StorageKind.Link,
            _ => StorageKind.Device,
        };
    }

    /// <summary>The icon of a storage kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A MudBlazor icon path.</returns>
    public static string Icon(StorageKind kind) => kind switch
    {
        StorageKind.Link => Icons.Material.Filled.Link,
        StorageKind.Account => Icons.Material.Filled.CloudDone,
        StorageKind.AccountShared => Icons.Material.Filled.GroupAdd,
        _ => Icons.Material.Filled.Smartphone,
    };

    /// <summary>The title key of a storage kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>A resource key.</returns>
    public static string LabelKey(StorageKind kind) => kind switch
    {
        StorageKind.Link => "ModeSharedTitle",
        StorageKind.Account => "ModeAccountTitle",
        StorageKind.AccountShared => "ModeAccountSharedTitle",
        _ => "ModeLocalTitle",
    };
}
