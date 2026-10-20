using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// What the sign-in import (ADR-0044, Q35) asks about and how it brings each ticked group into the account
/// (R11, 2026-10-20 device test). A plain local group is uploaded through the account store. A group shared by
/// link while signed out is claimed through its management link instead, so it stays shared — *My account ·
/// shared*, join link kept (ADR-0081) — rather than turning into a private account group that strands the people
/// who joined. A group this device only joined by link is the organizer's and is never offered.
/// </summary>
public static class SignInImportPlan
{
    /// <summary>The groups to list in the import prompt.</summary>
    /// <param name="accountFree">The account-free groups on this device.</param>
    /// <param name="keptLocal">Groups the user already chose to keep on this device.</param>
    /// <returns>The groups to ask about, in the given order.</returns>
    public static IReadOnlyList<ClaimableGroup> ToAsk(
        IReadOnlyList<ClaimableGroup> accountFree,
        IReadOnlySet<Guid> keptLocal)
    {
        ArgumentNullException.ThrowIfNull(accountFree);
        ArgumentNullException.ThrowIfNull(keptLocal);

        return [.. accountFree.Where(g => !g.IsJoinOnly && !keptLocal.Contains(g.Id))];
    }

    /// <summary>Splits the ticked groups by how they move into the account.</summary>
    /// <param name="asked">The groups the prompt listed.</param>
    /// <param name="ticked">The ids the user ticked.</param>
    /// <returns>The groups to upload to the account store and the shared groups to claim by management link.</returns>
    public static (IReadOnlyList<Guid> ToAccountStore, IReadOnlyList<Guid> SharedToClaim) Split(
        IReadOnlyList<ClaimableGroup> asked,
        IReadOnlyCollection<Guid> ticked)
    {
        ArgumentNullException.ThrowIfNull(asked);
        ArgumentNullException.ThrowIfNull(ticked);

        var chosen = asked.Where(g => ticked.Contains(g.Id)).ToList();
        return (
            [.. chosen.Where(g => g.Mode != StorageMode.SharedByLink).Select(g => g.Id)],
            [.. chosen.Where(g => g.Mode == StorageMode.SharedByLink).Select(g => g.Id)]);
    }
}
