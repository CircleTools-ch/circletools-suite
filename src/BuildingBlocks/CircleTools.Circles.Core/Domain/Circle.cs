using CircleTools.SharedKernel;

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// A standing set of people — a family, a friend-group — that every app in the suite can reuse
/// (ADR-0018). The aggregate root for memberships, pending invitations and decline records.
/// </summary>
/// <remarks>
/// Those three live <em>inside</em> the circle rather than beside it because every rule that
/// matters is circle-scoped: exactly one owner, a managed member is never an admin, one pending
/// invitation per address, and the caps. An invariant that spans aggregates cannot be enforced
/// without a lock, and a circle is at most a hundred people and fifty invitations, so loading the
/// whole thing is cheap.
/// <para>
/// The aggregate is pure: no clock, no randomness, no I/O. Callers supply <c>now</c> and the
/// secret handle, which is what makes the fourteen-day expiry and the seven-day cooldown testable
/// without waiting for real time to pass.
/// </para>
/// <para>
/// Role checks live here as well as in the API's endpoint filter. The filter is what makes a
/// non-member's answer identical to an unknown circle's; these are what make the rules true even
/// if a later endpoint forgets to ask.
/// </para>
/// </remarks>
public sealed class Circle : AggregateRoot
{
    private readonly List<CircleMembership> _memberships = [];
    private readonly List<Invitation> _invitations = [];
    private readonly List<DeclineRecord> _declines = [];

    // EF Core materialization constructor.
    private Circle()
    {
        Name = string.Empty;
        Label = string.Empty;
    }

    private Circle(Guid id, string name, string label, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Label = label;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the name that identifies the circle to its members.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the word the UI uses for this circle. User data: written once in the creator's
    /// language and never re-translated, so a friend-group is never told it is a "family".
    /// </summary>
    public string Label { get; private set; }

    /// <summary>Gets the moment the circle was created.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the people in the circle — account-backed and managed alike.</summary>
    public IReadOnlyList<CircleMembership> Memberships => _memberships.AsReadOnly();

    /// <summary>Gets the invitations awaiting an answer.</summary>
    public IReadOnlyList<Invitation> Invitations => _invitations.AsReadOnly();

    /// <summary>Gets the decline records that are suppressing re-invitations.</summary>
    public IReadOnlyList<DeclineRecord> Declines => _declines.AsReadOnly();

    /// <summary>Gets the circle's owner. A circle has exactly one, at all times.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the invariant has been broken.</exception>
    public CircleMembership Owner =>
        _memberships.SingleOrDefault(m => m.Role == CircleRole.Owner)
        ?? throw new InvalidOperationException($"Circle '{Id}' does not have exactly one owner.");

    /// <summary>Creates a circle and its owner's membership in one operation.</summary>
    /// <param name="ownerUserId">The creator, who becomes the sole owner.</param>
    /// <param name="ownerDisplayName">The creator's display name.</param>
    /// <param name="ownerLanguage">The creator's language, which decides the default label.</param>
    /// <param name="name">The circle's name. Required.</param>
    /// <param name="label">The label; blank falls back to the language default.</param>
    /// <param name="ownedCircleCount">How many circles this user already owns.</param>
    /// <param name="limits">The configured caps.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The circle, or the reason creation was refused.</returns>
    /// <remarks>
    /// The owner membership is not a separate step: an ownerless circle can never be administered,
    /// transferred or deleted by anyone, so it must never exist even for the width of a failure.
    /// </remarks>
    public static CircleResult<Circle> Create(
        Guid ownerUserId,
        string ownerDisplayName,
        string? ownerLanguage,
        string name,
        string? label,
        int ownedCircleCount,
        CircleLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrWhiteSpace(name))
        {
            return CircleResults.Refused<Circle>(CircleError.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(ownerDisplayName))
        {
            return CircleResults.Refused<Circle>(CircleError.DisplayNameRequired);
        }

        if (ownedCircleCount >= limits.OwnedCirclesPerUser)
        {
            return CircleResults.Refused<Circle>(CircleError.OwnedCircleLimitReached);
        }

        var resolvedLabel = string.IsNullOrWhiteSpace(label)
            ? CircleLabelDefaults.ForLanguage(ownerLanguage)
            : label.Trim();

        var circle = new Circle(GuidV7.New(), name.Trim(), resolvedLabel, now);
        circle._memberships.Add(
            CircleMembership.ForAccount(ownerUserId, ownerDisplayName, ownerLanguage, CircleRole.Owner, now));

        return CircleResults.Ok(circle);
    }

    /// <summary>Edits the circle's name and label (FR-020). Owner and admin.</summary>
    /// <param name="actorUserId">The user making the change.</param>
    /// <param name="name">The new name. Required.</param>
    /// <param name="label">The new label; blank leaves the current one in place.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    /// <remarks>
    /// A blank label keeps the stored one rather than re-defaulting to the editor's language.
    /// Re-defaulting would let an English-speaking admin silently turn a circle's <c>Familie</c>
    /// into <c>Family</c> — the retranslation FR-016 exists to prevent.
    /// </remarks>
    public CircleResult Update(Guid actorUserId, string name, string? label)
    {
        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return CircleResult.Refused(CircleError.NameRequired);
        }

        Name = name.Trim();

        if (!string.IsNullOrWhiteSpace(label))
        {
            Label = label.Trim();
        }

        return CircleResult.Success;
    }

    /// <summary>Invites an address to the circle (FR-017). Owner and admin.</summary>
    /// <param name="actorUserId">The inviter.</param>
    /// <param name="email">The address to invite.</param>
    /// <param name="resolvedUserId">
    /// The account behind that address if it has a verified one, otherwise <c>null</c>. Resolved
    /// outside the aggregate, because Identity is not the Circles context's to reach into.
    /// </param>
    /// <param name="managedMembershipId">The managed profile being promoted, when there is one.</param>
    /// <param name="secretHandle">The stored handle of a freshly minted link secret.</param>
    /// <param name="limits">The configured caps and the invitation lifetime.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>What to send, or the reason the invitation was refused.</returns>
    /// <remarks>
    /// The order of the checks is load-bearing. Everything that does not depend on the address —
    /// the caller's role and both caps — is decided first, so that an address with an account, one
    /// without, and one already in the circle reach the same answer by the same amount of work.
    /// The three address-dependent branches at the end all produce a <em>success</em>: an existing
    /// invitation is resent rather than duplicated, and a member's address or a recently declined
    /// one produces silence that the inviter cannot tell apart from a mail going out.
    /// </remarks>
    public CircleResult<InvitationDispatch> Invite(
        Guid actorUserId,
        string email,
        Guid? resolvedUserId,
        Guid? managedMembershipId,
        string secretHandle,
        CircleLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHandle);

        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return CircleResults.Refused<InvitationDispatch>(permission.Error!.Value);
        }

        if (!EmailAddress.IsValid(email))
        {
            return CircleResults.Refused<InvitationDispatch>(CircleError.EmailInvalid);
        }

        if (managedMembershipId is { } targetId)
        {
            var target = FindMembership(targetId);
            if (target is null || !target.IsManaged)
            {
                return CircleResults.Refused<InvitationDispatch>(CircleError.ManagedMemberNotFound);
            }
        }

        PurgeExpiredInvitations(now);

        var emailHash = EmailAddress.HashOf(email);
        var pending = _invitations.SingleOrDefault(i =>
            string.Equals(i.EmailHash, emailHash, StringComparison.Ordinal));

        if (pending is not null)
        {
            pending.RetargetTo(managedMembershipId);
            pending.Reissue(secretHandle, actorUserId, now, limits.InvitationLifetime);
            return CircleResults.Ok(InvitationDispatch.Send(pending));
        }

        if (_invitations.Count >= limits.PendingInvitationsPerCircle)
        {
            return CircleResults.Refused<InvitationDispatch>(CircleError.PendingInvitationLimitReached);
        }

        if (_memberships.Count >= limits.PeoplePerCircle)
        {
            return CircleResults.Refused<InvitationDispatch>(CircleError.CircleFull);
        }

        if (resolvedUserId is { } userId && _memberships.Any(m => m.UserId == userId))
        {
            return CircleResults.Ok(InvitationDispatch.Suppressed);
        }

        if (_declines.Any(d =>
                string.Equals(d.EmailHash, emailHash, StringComparison.Ordinal)
                && d.IsActiveAt(now, limits.DeclineCooldown)))
        {
            return CircleResults.Ok(InvitationDispatch.Suppressed);
        }

        var invitation = Invitation.Issue(
            email,
            actorUserId,
            managedMembershipId,
            secretHandle,
            now,
            limits.InvitationLifetime);

        _invitations.Add(invitation);
        return CircleResults.Ok(InvitationDispatch.Send(invitation));
    }

    /// <summary>Re-sends a pending invitation (FR-017). Owner and admin.</summary>
    /// <param name="actorUserId">The user resending.</param>
    /// <param name="invitationId">The invitation to resend.</param>
    /// <param name="secretHandle">The stored handle of a freshly minted link secret.</param>
    /// <param name="limits">The configured invitation lifetime.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The re-issued invitation, or the reason it was refused.</returns>
    public CircleResult<Invitation> Resend(
        Guid actorUserId,
        Guid invitationId,
        string secretHandle,
        CircleLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHandle);

        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return CircleResults.Refused<Invitation>(permission.Error!.Value);
        }

        PurgeExpiredInvitations(now);

        var invitation = FindInvitation(invitationId);
        if (invitation is null)
        {
            return CircleResults.Refused<Invitation>(CircleError.InvitationNotFound);
        }

        invitation.Reissue(secretHandle, actorUserId, now, limits.InvitationLifetime);
        return CircleResults.Ok(invitation);
    }

    /// <summary>Revokes a pending invitation (FR-017). Owner and admin.</summary>
    /// <param name="actorUserId">The user revoking.</param>
    /// <param name="invitationId">The invitation to revoke.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    public CircleResult Revoke(Guid actorUserId, Guid invitationId)
    {
        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var invitation = FindInvitation(invitationId);
        if (invitation is null)
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        _invitations.Remove(invitation);
        return CircleResult.Success;
    }

    /// <summary>
    /// Accepts an invitation (FR-018). The single accept path: both the mailed-link branch and the
    /// in-app join-request branch arrive here, so the rule that decides everything — the caller's
    /// <em>verified</em> address is the invited one — is checked in exactly one place.
    /// </summary>
    /// <param name="invitationId">The invitation being accepted.</param>
    /// <param name="userId">The signed-in account accepting it.</param>
    /// <param name="verifiedEmail">That account's verified address.</param>
    /// <param name="displayName">That account's display name.</param>
    /// <param name="language">That account's preferred language.</param>
    /// <param name="limits">The configured caps.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new or newly linked membership, or the reason it was refused.</returns>
    /// <remarks>
    /// A mismatching address is the forwarded-link case: it creates nothing, and the caller must
    /// not learn anything from it — in particular not whether the invited address has an account.
    /// </remarks>
    public CircleResult<CircleMembership> Accept(
        Guid invitationId,
        Guid userId,
        string verifiedEmail,
        string displayName,
        string? language,
        CircleLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedEmail);

        var invitation = FindInvitation(invitationId);
        if (invitation is null)
        {
            return CircleResults.Refused<CircleMembership>(CircleError.InvitationNotFound);
        }

        if (invitation.IsExpiredAt(now))
        {
            _invitations.Remove(invitation);
            return CircleResults.Refused<CircleMembership>(CircleError.InvitationExpired);
        }

        if (!string.Equals(invitation.EmailHash, EmailAddress.HashOf(verifiedEmail), StringComparison.Ordinal))
        {
            return CircleResults.Refused<CircleMembership>(CircleError.InvitationAddressMismatch);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return CircleResults.Refused<CircleMembership>(CircleError.DisplayNameRequired);
        }

        if (_memberships.Any(m => m.UserId == userId))
        {
            _invitations.Remove(invitation);
            return CircleResults.Refused<CircleMembership>(CircleError.AlreadyAMember);
        }

        // Promotion (FR-019): link the profile the person already has rather than adding a second
        // one, so they keep their place and their history. A profile removed since the invitation
        // was sent simply means there is nothing to link, and they join as a new person.
        var promoted = invitation.ManagedMembershipId is { } managedId ? FindMembership(managedId) : null;
        if (promoted is { IsManaged: true })
        {
            promoted.LinkToAccount(userId, displayName, language);
            _invitations.Remove(invitation);
            return CircleResults.Ok(promoted);
        }

        if (_memberships.Count >= limits.PeoplePerCircle)
        {
            return CircleResults.Refused<CircleMembership>(CircleError.CircleFull);
        }

        var membership = CircleMembership.ForAccount(userId, displayName, language, CircleRole.Member, now);
        _memberships.Add(membership);
        _invitations.Remove(invitation);

        return CircleResults.Ok(membership);
    }

    /// <summary>
    /// Declines an invitation (FR-018). Needs no account and no membership — anyone holding the
    /// link may decline, which is what stops "declined" from proving the address has an account.
    /// </summary>
    /// <param name="invitationId">The invitation being declined.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    public CircleResult Decline(Guid invitationId, DateTimeOffset now)
    {
        var invitation = FindInvitation(invitationId);
        if (invitation is null)
        {
            return CircleResult.Refused(CircleError.InvitationNotFound);
        }

        _invitations.Remove(invitation);
        _declines.RemoveAll(d => string.Equals(d.EmailHash, invitation.EmailHash, StringComparison.Ordinal));
        _declines.Add(DeclineRecord.For(invitation.EmailHash, now));

        return CircleResult.Success;
    }

    /// <summary>Adds a managed member — a person with a profile but no account (FR-019).</summary>
    /// <param name="actorUserId">The owner or admin adding them.</param>
    /// <param name="displayName">Their display name. Required, and not unique.</param>
    /// <param name="language">Their preferred language, if known. Nothing else is stored.</param>
    /// <param name="limits">The configured caps.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The new membership, or the reason it was refused.</returns>
    public CircleResult<CircleMembership> AddManagedMember(
        Guid actorUserId,
        string displayName,
        string? language,
        CircleLimits limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return CircleResults.Refused<CircleMembership>(permission.Error!.Value);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return CircleResults.Refused<CircleMembership>(CircleError.DisplayNameRequired);
        }

        if (_memberships.Count >= limits.PeoplePerCircle)
        {
            return CircleResults.Refused<CircleMembership>(CircleError.CircleFull);
        }

        var membership = CircleMembership.Managed(displayName, language, now);
        _memberships.Add(membership);

        return CircleResults.Ok(membership);
    }

    /// <summary>Edits a managed member's profile (FR-019).</summary>
    /// <param name="actorUserId">The owner or admin editing.</param>
    /// <param name="membershipId">The managed membership to edit.</param>
    /// <param name="displayName">The new display name. Required.</param>
    /// <param name="language">The new preferred language, or <c>null</c> to clear it.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    public CircleResult UpdateManagedMember(
        Guid actorUserId,
        Guid membershipId,
        string displayName,
        string? language)
    {
        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var membership = FindMembership(membershipId);
        if (membership is null || !membership.IsManaged)
        {
            return CircleResult.Refused(CircleError.ManagedMemberNotFound);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return CircleResult.Refused(CircleError.DisplayNameRequired);
        }

        membership.Rename(displayName);
        membership.SetLanguage(language);

        return CircleResult.Success;
    }

    /// <summary>Removes someone from the circle (FR-020).</summary>
    /// <param name="actorUserId">The owner or admin removing them.</param>
    /// <param name="membershipId">The membership to remove.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    /// <remarks>
    /// This deletes the membership and nothing else: the person's account, their Secret-Santa
    /// groups, their members and their completed draws are untouched.
    /// </remarks>
    public CircleResult Remove(Guid actorUserId, Guid membershipId)
    {
        var permission = Authorize(actorUserId, CircleRole.Admin);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var membership = FindMembership(membershipId);
        if (membership is null)
        {
            return CircleResult.Refused(CircleError.MembershipNotFound);
        }

        if (membership.Role == CircleRole.Owner)
        {
            return CircleResult.Refused(CircleError.OwnerCannotBeRemoved);
        }

        if (membership.UserId == actorUserId)
        {
            return CircleResult.Refused(CircleError.CannotRemoveYourself);
        }

        // An admin who can remove another admin can remove everyone between themselves and the
        // owner. Only the owner may.
        if (membership.Role == CircleRole.Admin && RoleOf(actorUserId) != CircleRole.Owner)
        {
            return CircleResult.Refused(CircleError.AdminCannotRemoveAdmin);
        }

        _memberships.Remove(membership);
        return CircleResult.Success;
    }

    /// <summary>Changes someone's role between member and admin (FR-020). Owner only.</summary>
    /// <param name="actorUserId">The owner making the change.</param>
    /// <param name="membershipId">The membership whose role changes.</param>
    /// <param name="role">The new role.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    /// <remarks>
    /// Owner-only because an admin who can appoint admins can manufacture a majority and make the
    /// owner's exclusive rights — transfer and deletion — negotiable.
    /// </remarks>
    public CircleResult ChangeRole(Guid actorUserId, Guid membershipId, CircleRole role)
    {
        var permission = Authorize(actorUserId, CircleRole.Owner);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        if (role == CircleRole.Owner)
        {
            return CircleResult.Refused(CircleError.OwnershipRequiresTransfer);
        }

        var membership = FindMembership(membershipId);
        if (membership is null)
        {
            return CircleResult.Refused(CircleError.MembershipNotFound);
        }

        if (membership.Role == CircleRole.Owner)
        {
            return CircleResult.Refused(CircleError.OwnerCannotBeDemoted);
        }

        // Admin implies someone who can sign in and act. A managed member cannot do either.
        if (membership.IsManaged && role != CircleRole.Member)
        {
            return CircleResult.Refused(CircleError.ManagedMemberCannotBeElevated);
        }

        membership.ChangeRole(role);
        return CircleResult.Success;
    }

    /// <summary>Transfers ownership (FR-020). Owner only, and immediate.</summary>
    /// <param name="actorUserId">The current owner.</param>
    /// <param name="membershipId">The membership receiving ownership.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    /// <remarks>
    /// One operation on one aggregate: the target becomes owner and the previous owner becomes
    /// admin in the same change, so it cannot half-happen. Transfer is immediate rather than an
    /// offer awaiting acceptance, because a pending offer is precisely the ownerless limbo the
    /// model forbids.
    /// </remarks>
    public CircleResult TransferOwnership(Guid actorUserId, Guid membershipId)
    {
        var permission = Authorize(actorUserId, CircleRole.Owner);
        if (!permission.IsSuccess)
        {
            return permission;
        }

        var target = FindMembership(membershipId);
        if (target is null)
        {
            return CircleResult.Refused(CircleError.MembershipNotFound);
        }

        if (target.Role == CircleRole.Owner)
        {
            return CircleResult.Refused(CircleError.AlreadyTheOwner);
        }

        if (target.IsManaged)
        {
            return CircleResult.Refused(CircleError.TransferTargetIsManaged);
        }

        Owner.ChangeRole(CircleRole.Admin);
        target.ChangeRole(CircleRole.Owner);

        return CircleResult.Success;
    }

    /// <summary>Leaves the circle (FR-021). Needs nobody's permission.</summary>
    /// <param name="actorUserId">The user leaving.</param>
    /// <returns>Success, or the reason it was refused.</returns>
    /// <remarks>
    /// The owner is the exception, and the way out depends on who else is there: with other
    /// account-backed people present ownership must be transferred first; with nobody who could
    /// own it, leaving means deleting the circle. Because the owner can neither leave an occupied
    /// circle nor be removed, a circle is never memberless — which is why nothing in the system has
    /// to define what an ownerless circle means.
    /// </remarks>
    public CircleResult Leave(Guid actorUserId)
    {
        var membership = FindMembershipOf(actorUserId);
        if (membership is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        if (membership.Role == CircleRole.Owner)
        {
            var someoneElseCouldOwnIt = _memberships.Any(m => !m.IsManaged && m.UserId != actorUserId);

            return CircleResult.Refused(
                someoneElseCouldOwnIt ? CircleError.OwnerMustTransferOwnership : CircleError.OwnerMustDeleteCircle);
        }

        _memberships.Remove(membership);
        return CircleResult.Success;
    }

    /// <summary>
    /// Removes every trace of a departing account (FR-015's second seam): their membership and the
    /// invitations they issued.
    /// </summary>
    /// <param name="userId">The account being deleted.</param>
    /// <remarks>
    /// Managed members are deliberately left alone. They are circle data, not their creator's data,
    /// and they survive the departure of the admin who added them (FR-019).
    /// <para>
    /// This takes no actor and refuses nothing: it runs inside account deletion, which has already
    /// been authorized and cannot be blocked here. A sole owner is stopped earlier, by the
    /// deletion guard, because by this point there is nothing left to stop.
    /// </para>
    /// </remarks>
    public void PurgeAccount(Guid userId)
    {
        _memberships.RemoveAll(m => m.UserId == userId);
        _invitations.RemoveAll(i => i.InvitedByUserId == userId);
    }

    /// <summary>Drops invitations that have passed their expiry (NFR-004).</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>How many were dropped.</returns>
    /// <remarks>
    /// An expired invitation leaves the pending list and frees the address to be invited again.
    /// Because a pending invitation is the only address M2 holds for someone who has not agreed to
    /// anything, it must not outlive its purpose — so this runs on the read paths as well as in the
    /// retention job.
    /// </remarks>
    public int PurgeExpiredInvitations(DateTimeOffset now) =>
        _invitations.RemoveAll(i => i.IsExpiredAt(now));

    /// <summary>Drops decline records whose cooldown has elapsed (NFR-004).</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="cooldown">The configured cooldown.</param>
    /// <returns>How many were dropped.</returns>
    public int PurgeElapsedDeclines(DateTimeOffset now, TimeSpan cooldown) =>
        _declines.RemoveAll(d => !d.IsActiveAt(now, cooldown));

    /// <summary>Finds a membership by its identity.</summary>
    /// <param name="membershipId">The membership identity.</param>
    /// <returns>The membership, or <c>null</c>.</returns>
    public CircleMembership? FindMembership(Guid membershipId) =>
        _memberships.FirstOrDefault(m => m.Id == membershipId);

    /// <summary>Finds the membership held by an account.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The membership, or <c>null</c> when the account is not in this circle.</returns>
    public CircleMembership? FindMembershipOf(Guid userId) =>
        _memberships.FirstOrDefault(m => m.UserId == userId);

    /// <summary>Finds a pending invitation by its identity.</summary>
    /// <param name="invitationId">The invitation identity.</param>
    /// <returns>The invitation, or <c>null</c>.</returns>
    public Invitation? FindInvitation(Guid invitationId) =>
        _invitations.FirstOrDefault(i => i.Id == invitationId);

    /// <summary>Finds a pending invitation by the stored handle of its link secret.</summary>
    /// <param name="secretHandle">The handle computed from the secret in the link.</param>
    /// <returns>The invitation, or <c>null</c>.</returns>
    public Invitation? FindInvitationByHandle(string secretHandle) =>
        _invitations.FirstOrDefault(i => string.Equals(i.SecretHandle, secretHandle, StringComparison.Ordinal));

    /// <summary>Gets an account's role in this circle.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The role, or <c>null</c> when the account is not a member.</returns>
    public CircleRole? RoleOf(Guid userId) => FindMembershipOf(userId)?.Role;

    /// <summary>Checks whether an account is this circle's owner and the only person who could be.</summary>
    /// <param name="userId">The account.</param>
    /// <returns><c>true</c> when nobody else could receive ownership.</returns>
    public bool IsSoleOwner(Guid userId) =>
        RoleOf(userId) == CircleRole.Owner && CountOtherAccountBackedPeople(userId) == 0;

    /// <summary>Counts the account-backed people other than one.</summary>
    /// <param name="userId">The account to exclude.</param>
    /// <returns>How many others could receive ownership.</returns>
    public int CountOtherAccountBackedPeople(Guid userId) =>
        _memberships.Count(m => !m.IsManaged && m.UserId != userId);

    /// <summary>
    /// Checks that an account is a member of this circle and that their role reaches a bar.
    /// </summary>
    /// <param name="actorUserId">The account acting.</param>
    /// <param name="required">The least role that may act.</param>
    /// <returns>
    /// Success; <see cref="CircleError.NotAMember"/>, which the API answers exactly as it answers an
    /// unknown circle; or <see cref="CircleError.RoleNotPermitted"/>, which reveals nothing the
    /// caller did not already know.
    /// </returns>
    public CircleResult Authorize(Guid actorUserId, CircleRole required)
    {
        var role = RoleOf(actorUserId);

        if (role is null)
        {
            return CircleResult.Refused(CircleError.NotAMember);
        }

        return role >= required ? CircleResult.Success : CircleResult.Refused(CircleError.RoleNotPermitted);
    }
}
