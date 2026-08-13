using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Core.Test.TestSupport;

namespace CircleTools.Circles.Core.Test;

/// <summary>
/// The <see cref="Circle"/> aggregate's invariants (TP-003). These are the rules that are cheap to
/// drive exhaustively here and expensive to discover in production: exactly one owner, an atomic
/// transfer, a managed member who is never elevated, one pending invitation per address, and the
/// caps.
/// </summary>
public sealed class CircleTests
{
    private static readonly Guid _owner = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid _admin = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid _member = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid _stranger = Guid.Parse("00000000-0000-0000-0000-0000000000a4");

    [Fact]
    public void Create_MakesTheCreatorTheSoleOwner()
    {
        var circle = CircleFactory.WithOwner(_owner);

        var membership = Assert.Single(circle.Memberships);
        Assert.Equal(_owner, membership.UserId);
        Assert.Equal(CircleRole.Owner, membership.Role);
        Assert.Equal(membership, circle.Owner);
    }

    [Theory]
    [InlineData("de-CH", CircleLabelDefaults.German)]
    [InlineData("en", CircleLabelDefaults.English)]
    [InlineData("fr-CH", CircleLabelDefaults.French)]
    [InlineData(null, CircleLabelDefaults.German)]
    public void Create_EmptyLabel_FallsBackToTheLanguageDefault(string? language, string expected)
    {
        var circle = CircleFactory.WithOwner(_owner, ownerLanguage: language, label: "   ");

        Assert.Equal(expected, circle.Label);
    }

    [Fact]
    public void Create_KeepsTheLabelItWasGiven()
    {
        var circle = CircleFactory.WithOwner(_owner, label: "  Tuesday climbing crew  ");

        Assert.Equal("Tuesday climbing crew", circle.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_IsRefused(string name)
    {
        var created = Circle.Create(
            _owner,
            "Anna",
            "de-CH",
            name,
            label: null,
            ownedCircleCount: 0,
            CircleFactory.Limits(),
            CircleFactory.Now);

        Assert.Equal(CircleError.NameRequired, created.Error);
    }

    [Fact]
    public void Create_BeyondTheOwnedCircleCap_IsRefused()
    {
        var limits = CircleFactory.Limits();

        var created = Circle.Create(
            _owner,
            "Anna",
            "de-CH",
            "Müller",
            label: null,
            limits.OwnedCirclesPerUser,
            limits,
            CircleFactory.Now);

        Assert.Equal(CircleError.OwnedCircleLimitReached, created.Error);
    }

    [Fact]
    public void Update_ByAMember_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.Equal(CircleError.RoleNotPermitted, circle.Update(_member, "Meier", null).Error);
    }

    [Fact]
    public void Update_ByANonMember_IsRefusedAsIfTheCircleWereUnknown()
    {
        var circle = CircleFactory.WithOwner(_owner);

        Assert.Equal(CircleError.NotAMember, circle.Update(_stranger, "Meier", null).Error);
    }

    [Fact]
    public void Update_WithABlankLabel_KeepsTheStoredOne()
    {
        var circle = CircleFactory.WithOwner(_owner, label: "Familie");

        Assert.True(circle.Update(_owner, "Meier", "  ").IsSuccess);
        Assert.Equal("Familie", circle.Label);
        Assert.Equal("Meier", circle.Name);
    }

    [Fact]
    public void Invite_ByAMember_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        var dispatch = circle.Invite(
            _member,
            "carla@example.com",
            resolvedUserId: null,
            managedMembershipId: null,
            InvitationSecret.Create().Handle,
            CircleFactory.Limits(),
            CircleFactory.Now);

        Assert.Equal(CircleError.RoleNotPermitted, dispatch.Error);
        Assert.Empty(circle.Invitations);
    }

    [Fact]
    public void Invite_SameAddressTwice_ResendsTheExistingInvitation()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var first = CircleFactory.Invite(circle, _owner, "carla@example.com");

        var second = CircleFactory.Invite(circle, _owner, "CARLA@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(circle.Invitations);
    }

    [Fact]
    public void Resend_IssuesAFreshSecretAndKillsThePreviousLink()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");
        var originalHandle = invitation.SecretHandle;
        var originalExpiry = invitation.ExpiresAt;

        var later = CircleFactory.Now.AddDays(3);
        var (_, freshHandle) = InvitationSecret.Create();
        var resent = circle.Resend(_owner, invitation.Id, freshHandle, CircleFactory.Limits(), later);

        Assert.Null(resent.Error);
        Assert.Equal(freshHandle, invitation.SecretHandle);
        Assert.True(invitation.ExpiresAt > originalExpiry);
        Assert.Null(circle.FindInvitationByHandle(originalHandle));
        Assert.NotNull(circle.FindInvitationByHandle(freshHandle));
    }

    /// <summary>
    /// Revoking is how an admin takes back an invitation sent to the wrong address (FR-017). It
    /// deletes rather than flags, so the link stops resolving and the address is free to be invited
    /// again — a revoked invitation left lying around as a row would keep holding an address the
    /// circle has no further business with (NFR-004).
    /// </summary>
    [Fact]
    public void Revoke_DeletesTheInvitation_SoItsLinkStopsWorkingAndTheAddressIsFreeAgain()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");
        var handle = invitation.SecretHandle;

        var revoked = circle.Revoke(_owner, invitation.Id);

        Assert.Null(revoked.Error);
        Assert.Empty(circle.Invitations);
        Assert.Null(circle.FindInvitationByHandle(handle));

        // Free again: the "one pending invitation per address" rule has nothing left to collide with.
        var again = CircleFactory.Invite(circle, _owner, "carla@example.com");
        Assert.NotEqual(invitation.Id, again.Id);
    }

    /// <summary>A revoked invitation is gone, so revoking it twice is not a second success.</summary>
    [Fact]
    public void Revoke_AnInvitationThatIsAlreadyGone_IsRefusedRatherThanSilentlyAccepted()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");

        circle.Revoke(_owner, invitation.Id);
        var second = circle.Revoke(_owner, invitation.Id);

        Assert.Equal(CircleError.InvitationNotFound, second.Error);
    }

    /// <summary>
    /// A member may not take back somebody else's invitation. The route filter already refuses them
    /// (FR-017 makes this owner-and-admin), and the aggregate refuses too — the check has to hold
    /// where the rule lives, not only where the request arrives.
    /// </summary>
    [Fact]
    public void Revoke_ByAMember_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");

        var refused = circle.Revoke(_member, invitation.Id);

        Assert.Equal(CircleError.RoleNotPermitted, refused.Error);
        Assert.Single(circle.Invitations);
    }

    /// <summary>
    /// FR-018's "an abandoned sign-up leaves the invitation pending": nothing but accepting or
    /// declining consumes an invitation, so someone who opens the link, starts registering and never
    /// finishes still has it waiting.
    /// </summary>
    [Fact]
    public void AnInvitationNobodyAnswered_StaysPendingAndItsLinkKeepsWorking()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");

        // Time passes — but less than the lifetime — and the circle is loaded again, which is where
        // expiry is swept.
        circle.PurgeExpiredInvitations(CircleFactory.Now.AddDays(13));

        Assert.Single(circle.Invitations);
        Assert.NotNull(circle.FindInvitationByHandle(invitation.SecretHandle));
    }

    [Fact]
    public void Invitation_PastItsExpiry_LeavesThePendingListAndFreesTheAddress()
    {
        var limits = CircleFactory.Limits();
        var circle = CircleFactory.WithOwner(_owner);
        var expired = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits);

        var afterExpiry = CircleFactory.Now + limits.InvitationLifetime + TimeSpan.FromMinutes(1);
        var reinvited = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits, now: afterExpiry);

        Assert.NotEqual(expired.Id, reinvited.Id);
        Assert.Single(circle.Invitations);
    }

    [Fact]
    public void Invitation_NeverGrantsMoreThanMember()
    {
        var circle = CircleFactory.WithOwner(_owner);

        var joined = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.Equal(CircleRole.Member, joined.Role);
    }

    [Fact]
    public void ReInviteInsideTheCooldown_LooksIdenticalAndSendsNothing()
    {
        var limits = CircleFactory.Limits();
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits);

        Assert.True(circle.Decline(invitation.Id, CircleFactory.Now).IsSuccess);

        var insideCooldown = CircleFactory.Now.AddDays(1);
        var dispatch = circle.Invite(
            _owner,
            "carla@example.com",
            resolvedUserId: null,
            managedMembershipId: null,
            InvitationSecret.Create().Handle,
            limits,
            insideCooldown);

        // Indistinguishable from a mail going out: the same success, and nothing to send.
        Assert.Null(dispatch.Error);
        Assert.NotNull(dispatch.Value);
        Assert.False(dispatch.Value.ShouldSend);
        Assert.Empty(circle.Invitations);
    }

    [Fact]
    public void ReInviteAfterTheCooldown_IsSentAgain()
    {
        var limits = CircleFactory.Limits();
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits);

        Assert.True(circle.Decline(invitation.Id, CircleFactory.Now).IsSuccess);

        var afterCooldown = CircleFactory.Now + limits.DeclineCooldown + TimeSpan.FromMinutes(1);
        var resent = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits, now: afterCooldown);

        Assert.NotEqual(invitation.Id, resent.Id);
    }

    [Fact]
    public void Invite_AnAddressAlreadyInTheCircle_SendsNothingAndSaysNothing()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        var dispatch = circle.Invite(
            _owner,
            "ben@example.com",
            resolvedUserId: _member,
            managedMembershipId: null,
            InvitationSecret.Create().Handle,
            CircleFactory.Limits(),
            CircleFactory.Now);

        Assert.Null(dispatch.Error);
        Assert.NotNull(dispatch.Value);
        Assert.False(dispatch.Value.ShouldSend);
        Assert.Empty(circle.Invitations);
        Assert.Equal(2, circle.Memberships.Count);
    }

    [Fact]
    public void Accept_BySomeoneElsesVerifiedAddress_CreatesNothing()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");

        var accepted = circle.Accept(
            invitation.Id,
            _stranger,
            "someone.else@example.com",
            "Someone Else",
            language: null,
            CircleFactory.Limits(),
            CircleFactory.Now);

        Assert.Equal(CircleError.InvitationAddressMismatch, accepted.Error);
        Assert.Single(circle.Memberships);
        Assert.Single(circle.Invitations);
    }

    [Fact]
    public void Accept_AfterExpiry_IsRefusedAndTheInvitationIsGone()
    {
        var limits = CircleFactory.Limits();
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits);

        var accepted = circle.Accept(
            invitation.Id,
            _member,
            "carla@example.com",
            "Carla",
            language: null,
            limits,
            CircleFactory.Now + limits.InvitationLifetime);

        Assert.Equal(CircleError.InvitationExpired, accepted.Error);
        Assert.Empty(circle.Invitations);
        Assert.Single(circle.Memberships);
    }

    [Fact]
    public void Accept_WhenAlreadyAMember_CreatesNoSecondMembership()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        var second = CircleFactory.Invite(circle, _owner, "ben.other@example.com");

        var accepted = circle.Accept(
            second.Id,
            _member,
            "ben.other@example.com",
            "Ben",
            language: null,
            CircleFactory.Limits(),
            CircleFactory.Now);

        Assert.Equal(CircleError.AlreadyAMember, accepted.Error);
        Assert.Equal(2, circle.Memberships.Count);
        Assert.Empty(circle.Invitations);
    }

    [Fact]
    public void Accept_ConsumesTheInvitation()
    {
        var circle = CircleFactory.WithOwner(_owner);

        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.Empty(circle.Invitations);
    }

    [Fact]
    public void ManagedMember_IsAddedInTheMemberRole_WithNoAddress()
    {
        var circle = CircleFactory.WithOwner(_owner);

        var managed = CircleFactory.AddManaged(circle, _owner, "  Mia  ", "de-CH");

        Assert.Equal("Mia", managed.DisplayName);
        Assert.Equal(CircleRole.Member, managed.Role);
        Assert.True(managed.IsManaged);
        Assert.Null(managed.UserId);
    }

    [Fact]
    public void ManagedMembers_MayShareADisplayName()
    {
        var circle = CircleFactory.WithOwner(_owner);

        CircleFactory.AddManaged(circle, _owner, "Anna");
        CircleFactory.AddManaged(circle, _owner, "Anna");

        Assert.Equal(2, circle.Memberships.Count(m => m.DisplayName == "Anna" && m.IsManaged));
    }

    [Theory]
    [InlineData(CircleRole.Admin)]
    [InlineData(CircleRole.Owner)]
    public void ManagedMember_CannotBeGivenAdminOrOwner(CircleRole role)
    {
        var circle = CircleFactory.WithOwner(_owner);
        var managed = CircleFactory.AddManaged(circle, _owner, "Mia");

        var changed = circle.ChangeRole(_owner, managed.Id, role);

        Assert.NotNull(changed.Error);
        Assert.Equal(CircleRole.Member, managed.Role);
    }

    [Fact]
    public void Promotion_LinksTheExistingMembership_AndCreatesNoDuplicate()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var managed = CircleFactory.AddManaged(circle, _owner, "Mia");

        var promoted = CircleFactory.Join(
            circle,
            _owner,
            _member,
            "mia@example.com",
            "Mia Meier",
            managedMembershipId: managed.Id);

        Assert.Equal(managed.Id, promoted.Id);
        Assert.Equal(2, circle.Memberships.Count);
        Assert.False(promoted.IsManaged);
        Assert.Equal(_member, promoted.UserId);
    }

    [Fact]
    public void Promotion_LetsTheAccountsDisplayNameWin()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var managed = CircleFactory.AddManaged(circle, _owner, "Mia");

        var promoted = CircleFactory.Join(
            circle,
            _owner,
            _member,
            "mia@example.com",
            "Mia Meier",
            managedMembershipId: managed.Id);

        Assert.Equal("Mia Meier", promoted.DisplayName);
    }

    [Fact]
    public void AdminChangingARole_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var admin = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        var member = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        Assert.True(circle.ChangeRole(_owner, admin.Id, CircleRole.Admin).IsSuccess);

        var changed = circle.ChangeRole(_admin, member.Id, CircleRole.Admin);

        Assert.Equal(CircleError.RoleNotPermitted, changed.Error);
        Assert.Equal(CircleRole.Member, member.Role);
    }

    [Fact]
    public void Admin_CannotRemoveAnotherAdmin()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var first = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        var second = CircleFactory.Join(circle, _owner, _member, "bea@example.com", "Bea");
        Assert.True(circle.ChangeRole(_owner, first.Id, CircleRole.Admin).IsSuccess);
        Assert.True(circle.ChangeRole(_owner, second.Id, CircleRole.Admin).IsSuccess);

        var removed = circle.Remove(_admin, second.Id);

        Assert.Equal(CircleError.AdminCannotRemoveAdmin, removed.Error);
        Assert.Equal(3, circle.Memberships.Count);
    }

    [Fact]
    public void Admin_MayRemoveAMemberAndAManagedMember()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var admin = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        var member = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        var managed = CircleFactory.AddManaged(circle, _owner, "Mia");
        Assert.True(circle.ChangeRole(_owner, admin.Id, CircleRole.Admin).IsSuccess);

        Assert.True(circle.Remove(_admin, member.Id).IsSuccess);
        Assert.True(circle.Remove(_admin, managed.Id).IsSuccess);

        Assert.Equal(2, circle.Memberships.Count);
    }

    [Fact]
    public void Owner_CannotBeRemovedOrDemoted_EvenByThemselves()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var ownership = circle.Owner;

        Assert.Equal(CircleError.OwnerCannotBeRemoved, circle.Remove(_owner, ownership.Id).Error);
        Assert.Equal(CircleError.OwnerCannotBeDemoted, circle.ChangeRole(_owner, ownership.Id, CircleRole.Member).Error);
        Assert.Equal(CircleRole.Owner, ownership.Role);
    }

    [Fact]
    public void ChangeRole_ToOwner_IsRefusedInFavourOfTransfer()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var member = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        var changed = circle.ChangeRole(_owner, member.Id, CircleRole.Owner);

        Assert.Equal(CircleError.OwnershipRequiresTransfer, changed.Error);
    }

    [Fact]
    public void Transfer_MakesTheTargetOwnerAndThePreviousOwnerAdmin_Atomically()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var target = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        var previous = circle.Owner;

        Assert.True(circle.TransferOwnership(_owner, target.Id).IsSuccess);

        Assert.Equal(CircleRole.Owner, target.Role);
        Assert.Equal(CircleRole.Admin, previous.Role);
        Assert.Equal(target, circle.Owner);
        Assert.Single(circle.Memberships, m => m.Role == CircleRole.Owner);
    }

    [Fact]
    public void Transfer_ToAManagedMember_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var managed = CircleFactory.AddManaged(circle, _owner, "Mia");

        var transferred = circle.TransferOwnership(_owner, managed.Id);

        Assert.Equal(CircleError.TransferTargetIsManaged, transferred.Error);
        Assert.Equal(_owner, circle.Owner.UserId);
    }

    [Fact]
    public void Transfer_ByAnAdmin_IsRefused()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var admin = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        var target = CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");
        Assert.True(circle.ChangeRole(_owner, admin.Id, CircleRole.Admin).IsSuccess);

        Assert.Equal(CircleError.RoleNotPermitted, circle.TransferOwnership(_admin, target.Id).Error);
        Assert.Equal(_owner, circle.Owner.UserId);
    }

    [Fact]
    public void Owner_WithOtherAccountBackedPeople_CannotLeave_AndIsOfferedTransfer()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.Equal(CircleError.OwnerMustTransferOwnership, circle.Leave(_owner).Error);
        Assert.Equal(2, circle.Memberships.Count);
    }

    [Fact]
    public void Owner_WithOnlyManagedMembers_IsOfferedDeletion()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.AddManaged(circle, _owner, "Mia");

        Assert.Equal(CircleError.OwnerMustDeleteCircle, circle.Leave(_owner).Error);
    }

    [Fact]
    public void Leave_DeletesTheMembership()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.True(circle.Leave(_member).IsSuccess);

        Assert.Single(circle.Memberships);
        Assert.Null(circle.RoleOf(_member));
    }

    [Fact]
    public void Leave_WhenAlreadyRemoved_ReportsNotAMember()
    {
        var circle = CircleFactory.WithOwner(_owner);

        Assert.Equal(CircleError.NotAMember, circle.Leave(_stranger).Error);
    }

    [Fact]
    public void Rejoining_AfterLeaving_ComesBackAsMember()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var admin = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        Assert.True(circle.ChangeRole(_owner, admin.Id, CircleRole.Admin).IsSuccess);
        Assert.True(circle.Leave(_admin).IsSuccess);

        var rejoined = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");

        Assert.Equal(CircleRole.Member, rejoined.Role);
    }

    [Fact]
    public void ADecline_StoresOnlyAHash()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var invitation = CircleFactory.Invite(circle, _owner, "carla@example.com");

        Assert.True(circle.Decline(invitation.Id, CircleFactory.Now).IsSuccess);

        var record = Assert.Single(circle.Declines);
        Assert.Equal(EmailAddress.HashOf("carla@example.com"), record.EmailHash);
        Assert.Empty(circle.Invitations);

        // Nothing on the record may still hold the address itself — the point of keeping a hash.
        var stored = typeof(DeclineRecord)
            .GetProperties()
            .Select(p => p.GetValue(record) as string)
            .Where(v => v is not null);

        Assert.DoesNotContain("carla@example.com", stored, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PurgeAccount_RemovesTheirMembership_ButLeavesManagedMembersInPlace()
    {
        var circle = CircleFactory.WithOwner(_owner);
        var admin = CircleFactory.Join(circle, _owner, _admin, "adam@example.com", "Adam");
        Assert.True(circle.ChangeRole(_owner, admin.Id, CircleRole.Admin).IsSuccess);
        CircleFactory.AddManaged(circle, _admin, "Mia");
        CircleFactory.Invite(circle, _admin, "carla@example.com");

        circle.PurgeAccount(_admin);

        Assert.Null(circle.RoleOf(_admin));
        Assert.Empty(circle.Invitations);
        Assert.Contains(circle.Memberships, m => m is { IsManaged: true, DisplayName: "Mia" });
    }

    [Fact]
    public void People_AreCappedPerCircle()
    {
        var limits = CircleFactory.Limits();
        limits.PeoplePerCircle = 2;
        var circle = CircleFactory.WithOwner(_owner, limits: limits);
        CircleFactory.AddManaged(circle, _owner, "Mia", limits: limits);

        var added = circle.AddManagedMember(_owner, "Noah", null, limits, CircleFactory.Now);

        Assert.Equal(CircleError.CircleFull, added.Error);
    }

    [Fact]
    public void PendingInvitations_AreCappedPerCircle()
    {
        var limits = CircleFactory.Limits();
        limits.PendingInvitationsPerCircle = 1;
        var circle = CircleFactory.WithOwner(_owner, limits: limits);
        CircleFactory.Invite(circle, _owner, "carla@example.com", limits: limits);

        var dispatch = circle.Invite(
            _owner,
            "dora@example.com",
            resolvedUserId: null,
            managedMembershipId: null,
            InvitationSecret.Create().Handle,
            limits,
            CircleFactory.Now);

        Assert.Equal(CircleError.PendingInvitationLimitReached, dispatch.Error);
    }

    [Fact]
    public void IsSoleOwner_IsTrueOnlyWhileNobodyElseCouldOwnIt()
    {
        var circle = CircleFactory.WithOwner(_owner);
        CircleFactory.AddManaged(circle, _owner, "Mia");

        Assert.True(circle.IsSoleOwner(_owner));

        CircleFactory.Join(circle, _owner, _member, "ben@example.com", "Ben");

        Assert.False(circle.IsSoleOwner(_owner));
    }
}
