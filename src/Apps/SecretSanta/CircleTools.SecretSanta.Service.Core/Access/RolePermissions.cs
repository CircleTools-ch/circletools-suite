namespace CircleTools.SecretSanta.Service.Core.Access;

/// <summary>
/// The one place that says what each role may do (ADR-0086). Adding a role — co-organizing admins, planned — means
/// adding a row here; endpoints and apps only ever ask for permissions.
/// </summary>
public static class RolePermissions
{
    private const GroupPermissions Owner =
        GroupPermissions.ReadDocument
        | GroupPermissions.WriteDocument
        | GroupPermissions.ManageParticipants
        | GroupPermissions.ManageLinks
        | GroupPermissions.ManageRoles
        | GroupPermissions.DeleteGroup;

    private const GroupPermissions Participant =
        GroupPermissions.ReadOwnView
        | GroupPermissions.WriteOwnEntry
        | GroupPermissions.Leave;

    /// <summary>
    /// What being a roster member adds to any role: reading one's own pairing. An organizer who is also in the roster
    /// sees their own assignment like any participant; their own entry stays part of the document they edit.
    /// </summary>
    public const GroupPermissions Member = GroupPermissions.ReadOwnView;

    /// <summary>The permissions a role grants.</summary>
    /// <param name="role">The role.</param>
    /// <returns>Its permissions; none for an unknown value.</returns>
    public static GroupPermissions For(GroupRole role) => role switch
    {
        GroupRole.Owner => Owner,
        GroupRole.Participant => Participant,
        _ => GroupPermissions.None,
    };
}
