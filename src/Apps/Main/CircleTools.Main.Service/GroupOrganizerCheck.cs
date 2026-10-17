using CircleTools.Main.Service.Core.Domain;
using Microsoft.AspNetCore.Http;

namespace CircleTools.Main.Service;

/// <summary>
/// Decides whether a signed-in caller organizes a Secret-Santa group, for the writes only the organizer may
/// make — the linking projection's event registry and assignment rows (ADR-0080, Q3).
/// </summary>
/// <remarks>
/// A group can live in three places, and the organizer is proven differently in each:
/// <list type="bullet">
/// <item>the caller's <strong>account sync store</strong> holds it (a <em>My account</em> group);</item>
/// <item>the <strong>shared-group store</strong> holds it: the caller owns it, or presents its active admin
/// token in <see cref="AdminTokenHeader"/>;</item>
/// <item><strong>neither</strong> holds it (an <em>On this device</em> group): the first account to publish
/// it is its organizer — rows already published by another account mean the caller is not.</item>
/// </list>
/// A group held by <em>another</em> account's sync store is never the caller's.
/// </remarks>
internal static class GroupOrganizerCheck
{
    /// <summary>The request header a client presents a shared group's admin token in.</summary>
    public const string AdminTokenHeader = "X-Admin-Token";

    /// <summary>Whether <paramref name="caller"/> organizes <paramref name="groupId"/>.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="caller">The signed-in caller.</param>
    /// <param name="request">The request, for the optional admin-token header.</param>
    /// <param name="sync">The account sync store.</param>
    /// <param name="shared">The shared-group store.</param>
    /// <param name="linking">The linking store.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when the caller may write the group's linking projection.</returns>
    public static async Task<bool> IsOrganizerAsync(
        Guid groupId,
        Guid caller,
        HttpRequest request,
        ISyncStore sync,
        ISharedGroupStore shared,
        ILinkingStore linking,
        CancellationToken cancellationToken)
    {
        var holding = await sync.GetHoldingAsync(groupId, caller, cancellationToken).ConfigureAwait(false);
        if (holding.HeldByAccount)
        {
            return true;
        }

        if (holding.HeldByOther)
        {
            return false;
        }

        var adminToken = request.Headers[AdminTokenHeader].ToString();
        var organizer = await shared
            .GetOrganizerAsync(groupId, string.IsNullOrWhiteSpace(adminToken) ? null : adminToken, cancellationToken)
            .ConfigureAwait(false);
        if (organizer is not null)
        {
            return organizer.OwnerAccountId == caller || organizer.AdminTokenValid;
        }

        var publishers = await linking.GetGroupPublishersAsync(groupId, cancellationToken).ConfigureAwait(false);
        return publishers.All(p => p == caller);
    }
}
