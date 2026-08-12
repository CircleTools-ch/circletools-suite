using AppSuite.Circles.Contracts;
using AppSuite.Circles.Core.Domain;

namespace AppSuite.Circles.Core.Application;

/// <summary>
/// Turns circles into the shapes the API returns.
/// </summary>
/// <remarks>
/// This is the single place a person becomes a DTO, which is what makes NFR-006's "no email
/// addresses" checkable by reading one file rather than seven endpoints.
/// </remarks>
internal static class CircleMapper
{
    /// <summary>The role name used on the wire — stable, lowercase, and not the enum's ToString.</summary>
    public static string NameOf(CircleRole role) => role switch
    {
        CircleRole.Owner => "owner",
        CircleRole.Admin => "admin",
        _ => "member",
    };

    /// <summary>Parses a wire role name. Owner is absent on purpose: ownership moves by transfer.</summary>
    public static CircleRole? ParseRole(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "ADMIN" => CircleRole.Admin,
        "MEMBER" => CircleRole.Member,
        _ => null,
    };

    public static CircleSummaryResponse ToSummary(Circle circle, Guid viewerUserId) =>
        new(
            circle.Id,
            circle.Name,
            circle.Label,
            NameOf(circle.RoleOf(viewerUserId) ?? CircleRole.Member),
            circle.Memberships.Count);

    public static CircleDetailResponse ToDetail(Circle circle, Guid viewerUserId) =>
        new(
            circle.Id,
            circle.Name,
            circle.Label,
            NameOf(circle.RoleOf(viewerUserId) ?? CircleRole.Member),
            [.. circle.Memberships.Select(m => ToPerson(m, viewerUserId))]);

    public static CirclePersonResponse ToPerson(CircleMembership membership, Guid viewerUserId) =>
        new(
            membership.Id,
            membership.DisplayName,
            NameOf(membership.Role),
            membership.IsManaged,
            membership.UserId == viewerUserId,
            // Account-backed members expose their opaque account id (never an address) so a group made
            // from this circle can link them; managed members have no account (FR-027, M4).
            membership.IsManaged ? null : membership.UserId);

    /// <summary>
    /// The pending list, without the ones that have quietly lapsed. Expired invitations are removed
    /// for good by the retention job; filtering here means a read never shows an invitation that
    /// can no longer be accepted, whichever runs first.
    /// </summary>
    public static IReadOnlyList<PendingInvitationResponse> ToPendingInvitations(
        Circle circle,
        DateTimeOffset now) =>
        [.. circle.Invitations
            .Where(i => !i.IsExpiredAt(now))
            .OrderBy(i => i.CreatedAt)
            .Select(i => new PendingInvitationResponse(i.Id, i.Email, i.CreatedAt, i.ExpiresAt))];

    public static SoleOwnedCircleResponse ToSoleOwned(Circle circle, Guid ownerUserId) =>
        new(circle.Id, circle.Name, circle.Label, circle.CountOtherAccountBackedPeople(ownerUserId));

    /// <summary>
    /// The display name to show for whoever sent an invitation. Their membership is the truth while
    /// they are still in the circle; if they have since left or deleted their account, the circle
    /// speaks for itself rather than the recipient seeing a blank.
    /// </summary>
    public static string InviterDisplayName(Circle circle, Invitation invitation) =>
        circle.FindMembershipOf(invitation.InvitedByUserId)?.DisplayName ?? circle.Owner.DisplayName;
}
