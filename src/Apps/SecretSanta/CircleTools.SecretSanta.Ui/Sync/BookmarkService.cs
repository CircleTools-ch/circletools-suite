// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.LocalStore;
using CircleTools.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Reads and toggles the signed-in account's pinned groups — the quick-access bookmarks shown in the
/// navigation (FR-033, ADR-0041). Pins are a per-account UI preference, not group state, so they live in
/// the preferences document that rides the sync store rather than on the <c>Group</c> aggregate; a toggle
/// is an offline-first local write that the next sync cycle pushes and the account's other devices adopt.
/// </summary>
/// <param name="local">The local store holding the preferences document.</param>
/// <param name="auth">Resolves the signed-in account the preferences are scoped to.</param>
public sealed class BookmarkService(ILocalSyncStore local, AuthenticationStateProvider auth)
{
    /// <summary>The signed-in account's pinned group ids, in pin order; empty when signed out or unset.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pinned group ids scoped to the current account.</returns>
    public async Task<IReadOnlyList<Guid>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        var accountId = await CurrentAccountAsync();
        if (accountId == Guid.Empty)
        {
            return [];
        }

        var record = await local.LoadPreferencesAsync(cancellationToken);

        // The document is scoped to one account; another account's copy on the same device is not ours.
        return record?.AccountScope == accountId ? record.State.PinnedGroupIds : [];
    }

    /// <summary>Pins the group if it is not pinned, unpins it if it is — a local, offline-first edit.</summary>
    /// <param name="groupId">The group to pin or unpin.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> if the group is now pinned, <see langword="false"/> if it was unpinned or the caller is signed out.</returns>
    public async Task<bool> ToggleAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var accountId = await CurrentAccountAsync();
        if (accountId == Guid.Empty)
        {
            return false;
        }

        var record = await local.LoadPreferencesAsync(cancellationToken);
        var mine = record?.AccountScope == accountId ? record : null;

        var pins = mine is null ? [] : new List<Guid>(mine.State.PinnedGroupIds);
        var pinned = !pins.Remove(groupId);
        if (pinned)
        {
            pins.Add(groupId);
        }

        // Bump the version and mark dirty; the next sync cycle pushes the edit and clears the flag.
        var version = (mine?.Version ?? 0) + 1;
        await local.SavePreferencesAsync(
            new SyncedPreferencesRecord(new PreferencesState(pins), version, accountId, Dirty: true),
            cancellationToken);

        return pinned;
    }

    private async Task<Guid> CurrentAccountAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        return state.User.UserId();
    }
}
