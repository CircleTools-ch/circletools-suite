using AppSuite.ApiClient;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.LocalStore.Linking;

namespace AppSuite.SecretSanta.Ui;

/// <summary>
/// Applies the member-link resolutions the server has for the organizer's groups onto the local group
/// documents (FEAT-005): an accepted or self-linked member gets its <c>UserId</c> set, an unlinked or
/// account-deletion-cascaded member gets it cleared. This is how a link reaches the authoritative
/// roster with no second poller — it rides the ordinary sync cycle, drains, applies and acknowledges,
/// then the modified group syncs like any other edit.
/// </summary>
/// <remarks>
/// A resolution for a group this device does not hold is left un-acknowledged, so the device that does
/// hold it can still apply it. A resolution the domain refuses (for example a user already linked to a
/// different member) is treated as consumed and acknowledged, so a single bad row can never wedge the
/// drain — the same "never let one item stall the cycle" rule M3's sync store follows.
/// </remarks>
/// <param name="api">The linking surface client.</param>
/// <param name="repository">The local group repository.</param>
public sealed class LinkResolutionApplier(ILinkingApi api, IGroupRepository repository)
{
    /// <summary>Drains, applies and acknowledges the pending link resolutions for the signed-in account.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many resolutions were applied to a local group. Zero on an unreachable server.</returns>
    public async Task<int> ApplyPendingAsync(CancellationToken cancellationToken = default)
    {
        var drained = await api.DrainResolutionsAsync(cancellationToken);
        if (!drained.IsSuccess || drained.Value!.Resolutions.Count == 0)
        {
            return 0;
        }

        var applied = new List<Guid>();

        foreach (var resolution in drained.Value.Resolutions)
        {
            var group = await repository.GetAsync(resolution.GroupId, cancellationToken);
            if (group is null)
            {
                // Not this device's group — leave it for the device that has it.
                continue;
            }

            if (TryApply(group, resolution))
            {
                await repository.SaveAsync(group, cancellationToken);
            }

            // Applied or refused-as-consumed: either way the row is done and must be acknowledged so it
            // stops being redelivered.
            applied.Add(resolution.Id);
        }

        if (applied.Count > 0)
        {
            await api.AckResolutionsAsync(applied, cancellationToken);
        }

        return applied.Count;
    }

    private static bool TryApply(Group group, MemberLinkResolution resolution)
    {
        try
        {
            if (resolution.Kind == MemberLinkResolutionKind.Linked && resolution.UserId is { } userId)
            {
                group.LinkMember(resolution.MemberId, userId);
                return true;
            }

            group.UnlinkMember(resolution.MemberId);
            return true;
        }
        catch (InvalidOperationException)
        {
            // The member is gone, or the account already backs another member of the group. The
            // resolution is still consumed; the group is left as it is.
            return false;
        }
    }
}
