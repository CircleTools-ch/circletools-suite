namespace AppSuite.Circles.Core.Domain;

/// <summary>
/// What a person may do in a circle (FR-020, ADR-0018). Ordered by authority, so
/// <c>role &gt;= CircleRole.Admin</c> is the check "may administer this circle".
/// </summary>
public enum CircleRole
{
    /// <summary>Sees the circle and its people, and may leave. The only role an invitation grants.</summary>
    Member = 0,

    /// <summary>
    /// Everything a member may do, plus editing the circle, inviting, managing managed members and
    /// removing members. May not change roles, remove another admin, transfer or delete.
    /// </summary>
    Admin = 1,

    /// <summary>
    /// Everything an admin may do, plus role changes, removing admins, transfer and deletion.
    /// A circle has exactly one owner at all times.
    /// </summary>
    Owner = 2,
}
