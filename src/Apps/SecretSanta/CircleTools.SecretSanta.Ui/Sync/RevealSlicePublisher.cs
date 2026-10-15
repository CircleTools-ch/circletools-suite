using System.Security.Cryptography;
using System.Text.Json;
using CircleTools.Application;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Reveal;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Publishes a backend-backed group's per-giver reveal slices so its reveal links show the current
/// wishlist and event details (ADR-0068, FEAT-020). Only a draw-capable device — a shared-by-link
/// admin-link holder, or an account owner — can build a slice (it needs the draw and every wishlist),
/// so this is where the "organizer re-publishes on reconcile" freshness model lives.
/// </summary>
/// <remarks>
/// Reveal tokens are minted once per (event, giver) and persisted on the group record, so a reveal link
/// stays stable across re-draws — the slice content changes, the link does not. The published set is
/// authoritative: a re-draw that drops a giver drops their slice server-side too. Every publish is
/// best-effort, like the rest of the background sync: a failure is swallowed and retried next reconcile
/// (NFR-008), so it never leaves local state half-changed or blocks the UI.
/// </remarks>
/// <param name="sender">The application dispatcher, for the group + reveal-slice queries.</param>
/// <param name="local">The shared local-store gateway (reveal tokens live on the group record).</param>
/// <param name="api">The anonymous shared-group API (admin-token publish).</param>
/// <param name="accountApi">The authorized shared-group API (account-owner publish).</param>
public sealed class RevealSlicePublisher(
    ISender sender,
    ILocalSyncStore local,
    ISharedGroupApi api,
    ISharedGroupAccountApi accountApi)
{
    /// <summary>
    /// Ensures the group's reveal tokens exist and publishes the current slice set (ADR-0068). A no-op —
    /// and never a throw — for a group that cannot publish (on-this-device, or a join-only shared holder).
    /// </summary>
    /// <param name="groupId">The group whose reveal slices to publish.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    public async Task PublishAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            await PublishCoreAsync(groupId, cancellationToken);
        }
        catch
        {
            // Best-effort by design (NFR-008): retried on the next reconcile, nothing lost.
        }
    }

    private async Task PublishCoreAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var records = (await local.LoadGroupsAsync(cancellationToken)).ToList();
        var index = records.FindIndex(r => r.State.Id == groupId);
        if (index < 0)
        {
            return;
        }

        var record = records[index];
        var adminToken = record.Tokens?.AdminToken;

        // Publish only from a device that can write slices: a shared-by-link admin-link holder, or the
        // owner of an admin-claimed shared group. A join-only holder, an on-this-device group and an
        // account-store group (no slice store; its links embed the snapshot) publish nothing —
        // ADR-0078 Amendment 3.
        var canPublishByAdmin = record.Mode == StorageMode.SharedByLink && adminToken is not null;
        var canPublishByOwner = record.IsAdminClaimedShared();
        if (!canPublishByAdmin && !canPublishByOwner)
        {
            return;
        }

        var group = await sender.Send(new GetGroupQuery(groupId), cancellationToken);
        if (group is null)
        {
            return;
        }

        var existing = record.Tokens?.RevealTokens ?? [];
        var tokens = new List<RevealTokenRef>();
        var slices = new List<RevealSlice>();

        foreach (var occasion in group.Events.Where(e => e.HasCurrentDraw))
        {
            var sliceData = await sender.Send(new GetRevealSlicesQuery(groupId, occasion.Id), cancellationToken);
            foreach (var slice in sliceData)
            {
                // Reuse a stable token for this (event, giver) if one exists; mint one otherwise.
                var token = existing.FirstOrDefault(t => t.EventId == occasion.Id && t.MemberId == slice.GiverMemberId)?.Token
                            ?? NewRevealToken();
                tokens.Add(new RevealTokenRef(occasion.Id, slice.GiverMemberId, token));

                var body = JsonSerializer.SerializeToElement(slice.Payload, RevealJsonContext.Default.RevealPayload);
                slices.Add(new RevealSlice(token, body));
            }
        }

        // Persist the token set when it changed (a mint or a removal), so links stay stable and the share
        // screen can attach rt= immediately.
        var changed = existing.Count != tokens.Count
            || !existing.All(e => tokens.Any(t => t.EventId == e.EventId && t.MemberId == e.MemberId && t.Token == e.Token));
        if (changed)
        {
            // Re-read before writing: only the reveal tokens change here. Writing back the copy read before the
            // queries above would undo an edit made meanwhile (a draw, a member added).
            var current = (await local.LoadGroupsAsync(cancellationToken)).ToList();
            var at = current.FindIndex(r => r.State.Id == groupId);
            if (at >= 0)
            {
                var updatedTokens = (current[at].Tokens ?? new SharedGroupTokens(null, null))
                    with { RevealTokens = tokens.Count > 0 ? tokens : null };
                current[at] = current[at] with { Tokens = updatedTokens };
                await local.SaveGroupsAsync(current, cancellationToken);
            }
        }

        // Publish the whole set (authoritative). Best-effort — a failure retries next reconcile.
        var request = new PublishRevealSlicesRequest(slices);
        if (canPublishByAdmin)
        {
            await api.PublishRevealSlicesAsync(adminToken!, request, cancellationToken);
        }
        else
        {
            await accountApi.PublishOwnedRevealSlicesAsync(groupId, request, cancellationToken);
        }
    }

    /// <summary>Mints a fresh, unguessable reveal token (256 bits, URL-safe), matching the share-token style (ADR-0058/0068).</summary>
    private static string NewRevealToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
