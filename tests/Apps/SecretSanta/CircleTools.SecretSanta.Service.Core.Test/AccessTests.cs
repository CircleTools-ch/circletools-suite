using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;

namespace CircleTools.SecretSanta.Service.Core.Test;

/// <summary>
/// Roles grant permissions in one place (ADR-0086), so a later admin role is a new row there. These pin the two
/// roles that exist today and the rule that an invitation grants nothing until it is accepted.
/// </summary>
public sealed class AccessTests
{
    [Fact]
    public void TheOwner_OrganizesButHasNoParticipantView()
    {
        var owner = Assignment(GroupRole.Owner, RoleAssignmentState.Active);

        Assert.True(owner.Allows(GroupPermissions.ReadDocument | GroupPermissions.WriteDocument));
        Assert.True(owner.Allows(GroupPermissions.ManageParticipants));
        Assert.True(owner.Allows(GroupPermissions.DeleteGroup));
        Assert.False(owner.Allows(GroupPermissions.ReadOwnView));
    }

    [Fact]
    public void AnOwnerInTheRoster_AlsoReadsTheirOwnView_ButKeepsTheirEntryInTheDocument()
    {
        var owner = Assignment(GroupRole.Owner, RoleAssignmentState.Active) with { MemberId = Guid.NewGuid() };

        Assert.True(owner.Allows(GroupPermissions.ReadOwnView));
        Assert.False(owner.Allows(GroupPermissions.WriteOwnEntry));
        Assert.False(owner.Allows(GroupPermissions.Leave));
    }

    [Fact]
    public void AParticipant_OnlyReadsTheirViewAndWritesTheirEntry()
    {
        var participant = Assignment(GroupRole.Participant, RoleAssignmentState.Active);

        Assert.True(participant.Allows(GroupPermissions.ReadOwnView));
        Assert.True(participant.Allows(GroupPermissions.WriteOwnEntry));
        Assert.False(participant.Allows(GroupPermissions.ReadDocument));
        Assert.False(participant.Allows(GroupPermissions.WriteDocument));
        Assert.False(participant.Allows(GroupPermissions.ManageParticipants));
    }

    [Fact]
    public void AnInvitation_GrantsNothingUntilAccepted()
    {
        var invited = Assignment(GroupRole.Participant, RoleAssignmentState.Invited);

        Assert.Equal(GroupPermissions.None, invited.Permissions);
        Assert.False(invited.Allows(GroupPermissions.ReadOwnView));
    }

    [Fact]
    public void NoPermission_IsNeverAllowed() =>
        Assert.False(Assignment(GroupRole.Owner, RoleAssignmentState.Active).Allows(GroupPermissions.None));

    [Fact]
    public void PermissionNames_AreCamelCased() =>
        Assert.Equal(["readOwnView", "writeOwnEntry", "leave"], GroupPermissionNames.Of(RolePermissions.For(GroupRole.Participant)));

    [Fact]
    public void TheDocument_RoundTripsThroughItsWireFormat()
    {
        var (document, _, _) = TestGroups.Drawn();

        Assert.True(GroupDocument.TryDeserialize(GroupDocument.Serialize(document), out var read));
        Assert.Equal(GroupDocument.Serialize(document), GroupDocument.Serialize(read!));
        Assert.False(GroupDocument.TryDeserialize("{not json", out _));
        Assert.False(GroupDocument.TryDeserialize("{}", out _));
    }

    private static RoleAssignment Assignment(GroupRole role, RoleAssignmentState state) =>
        new(Guid.NewGuid(), Guid.NewGuid(), role, PrincipalKind.Account, Guid.NewGuid(), null, state, AssignmentRoute.Created, DateTimeOffset.UnixEpoch);
}
