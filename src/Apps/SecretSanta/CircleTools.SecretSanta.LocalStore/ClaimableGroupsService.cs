namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// A group as the claim and sync-status screens need to see it (FR-025, FR-026): its name, its size,
/// whether it carries a current draw, and whether it is still account-free.
/// </summary>
/// <remarks>
/// This is the read half the M3 UI needs and the domain deliberately does not carry: the account
/// scope lives only in the LocalStore's <see cref="SyncedGroupRecord"/>, kept out of the
/// <see cref="CircleTools.SecretSanta.Core.Domain.Group"/> aggregate and its
/// <see cref="CircleTools.SecretSanta.Core.Persistence.GroupState"/> memento by ADR-0016 (the
/// <c>SyncLayeringTests</c> fitness function makes it a build rule). Reading it here — over the same
/// port the sync engine uses — keeps sync behind the LocalStore boundary rather than leaking it into
/// Core or the contracts.
/// </remarks>
/// <param name="Id">The group's identity.</param>
/// <param name="Name">The group's name, shown as-is (never translated).</param>
/// <param name="MemberCount">How many members the group has, for the count shown on the claim list.</param>
/// <param name="HasCurrentDraw">
/// Whether the group carries a valid current draw whose assignments would be uploaded on a claim
/// (NFR-002). A draw discarded by a later membership change does not count.
/// </param>
/// <param name="IsAccountFree">
/// Whether the group belongs to no account: not claimed, not marked "My account", and not a shared group
/// whose admin an account claimed (ADR-0078). The claim screen and the sign-in import prompt list exactly
/// these; the sync-status and un-claim surfaces show their complement.
/// </param>
/// <param name="Mode">
/// The group's storage mode (ADR-0058), so mode-aware surfaces (the hub chip, Sharing &amp; sync) can
/// show it without reaching into the sync record directly.
/// </param>
/// <param name="AccountScope">
/// The account the group is claimed into through the account sync store, or <see langword="null"/>.
/// </param>
/// <param name="Owner">
/// The account the group belongs to, of whichever "My account" kind: claimed, marked and waiting to be
/// claimed, or a shared group whose admin the account claimed (ADR-0078). <see langword="null"/> for an
/// account-free group — and for a legacy "My account" record written before the account was recorded. A
/// listing shows a signed-in user only their own account's groups, never another account's cached on
/// this device.
/// </param>
/// <param name="IsAwaitingClaim">
/// Whether the group is marked "My account" but not yet claimed through the account sync store and is
/// not an admin-claimed shared group — what the auto-claim uploads (ADR-0073, ADR-0078).
/// </param>
public sealed record ClaimableGroup(
    Guid Id,
    string Name,
    int MemberCount,
    bool HasCurrentDraw,
    bool IsAccountFree,
    StorageMode Mode,
    Guid? AccountScope = null,
    Guid? Owner = null,
    bool IsAwaitingClaim = false);

/// <summary>
/// Reads the local groups as the claim / un-claim / sync-status screens need them (FEAT-004), over the
/// same <see cref="ILocalSyncStore"/> the sync engine uses.
/// </summary>
/// <param name="local">The shared local-store gateway.</param>
public sealed class ClaimableGroupsService(ILocalSyncStore local)
{
    /// <summary>Reads every local group with the sync facts the M3 screens display.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Every local group, each flagged account-free or not, in stored order.</returns>
    public async Task<IReadOnlyList<ClaimableGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var records = await local.LoadGroupsAsync(cancellationToken);

        return [.. records.Select(Describe)];
    }

    /// <summary>Reads the account-free local groups the claim screen offers (FR-025).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups not yet claimed into any account, in stored order.</returns>
    public async Task<IReadOnlyList<ClaimableGroup>> GetAccountFreeAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);

        return [.. all.Where(g => g.IsAccountFree)];
    }

    /// <summary>
    /// Reads the "My account" groups the auto-claim should upload into <paramref name="accountId"/>
    /// (ADR-0073, ADR-0078): marked for this account and not claimed yet. Never an admin-claimed shared
    /// group (it already lives in the shared-group store) and never one marked for another account.
    /// </summary>
    /// <param name="accountId">The signed-in account.</param>
    /// <param name="includeUnattributed">
    /// Whether to include legacy records marked "My account" before the account was recorded. Pass
    /// <see langword="true"/> only once this cycle's owned-group discovery reached the server, so a legacy
    /// admin-claimed shared group has been recognised (and attributed) first and is not uploaded twice.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups to claim, in stored order.</returns>
    public async Task<IReadOnlyList<ClaimableGroup>> GetAwaitingClaimAsync(
        Guid accountId,
        bool includeUnattributed,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);

        return [.. all.Where(g => g.IsAwaitingClaim && (g.Owner == accountId || (includeUnattributed && g.Owner is null)))];
    }

    private static ClaimableGroup Describe(SyncedGroupRecord record)
    {
        var state = record.State;

        // A current draw is the most recent draw that a later membership change has not discarded —
        // the same "has a usable draw" reading the app uses, and what makes the claim screen warn that
        // assignments would be uploaded (NFR-002).
        var hasCurrentDraw = state.Draws.Count > 0 && !state.DrawInvalidatedByMembershipChange;

        // Which account the group belongs to, whichever "My account" kind it is (ADR-0078). A legacy
        // record in "My account" mode with nothing recorded still belongs to *some* account, so it is never
        // account-free — it is hidden while signed out and resolved on the next signed-in cycle.
        var owner = record.AccountScope ?? record.AdminAccount ?? record.PendingClaimAccount;

        return new ClaimableGroup(
            state.Id,
            state.Name,
            state.Members.Count,
            hasCurrentDraw,
            IsAccountFree: owner is null && record.Mode != StorageMode.Account,
            record.Mode,
            AccountScope: record.AccountScope,
            Owner: owner,
            IsAwaitingClaim: record.Mode == StorageMode.Account && record.AccountScope is null && record.AdminAccount is null);
    }
}
