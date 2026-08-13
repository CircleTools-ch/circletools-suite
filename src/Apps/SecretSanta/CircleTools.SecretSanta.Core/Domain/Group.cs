using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Aggregate root for a Secret-Santa group. Owns its members, exclusions and draw history, and
/// enforces the group's invariants (non-empty name, unique member names, exclusion endpoints that
/// are members, and at least two members before a draw).
/// </summary>
public sealed class Group : AggregateRoot
{
    private readonly List<Member> _members;
    private readonly List<Exclusion> _exclusions;
    private readonly List<Draw> _draws;

    private Group(
        Guid id,
        string name,
        Budget? budget,
        EventDetails? eventDetails,
        List<Member> members,
        List<Exclusion> exclusions,
        List<Draw> draws,
        bool drawInvalidatedByMembershipChange)
    {
        Id = id;
        Name = name;
        Budget = budget;
        EventDetails = eventDetails;
        _members = members;
        _exclusions = exclusions;
        _draws = draws;
        DrawInvalidatedByMembershipChange = drawInvalidatedByMembershipChange;
    }

    /// <summary>Gets the group name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the optional gift budget.</summary>
    public Budget? Budget { get; private set; }

    /// <summary>Gets the optional event details.</summary>
    public EventDetails? EventDetails { get; private set; }

    /// <summary>Gets the group members.</summary>
    public IReadOnlyList<Member> Members => _members.AsReadOnly();

    /// <summary>Gets the configured exclusions.</summary>
    public IReadOnlyList<Exclusion> Exclusions => _exclusions.AsReadOnly();

    /// <summary>Gets the ordered draw history (oldest first).</summary>
    public IReadOnlyList<Draw> Draws => _draws.AsReadOnly();

    /// <summary>Gets the current (most recent) draw, or <see langword="null"/> if none.</summary>
    public Draw? CurrentDraw => _draws.Count > 0 ? _draws[^1] : null;

    /// <summary>
    /// Gets a value indicating whether a draw was discarded because the member set changed. A draw
    /// belongs to the exact member set it was generated for, so adding or removing a member voids
    /// it (see <see cref="AddMember"/> and <see cref="RemoveMember"/>). This flag lets the UI tell
    /// "never drawn" apart from "drawn, then invalidated"; it is cleared by the next successful draw.
    /// </summary>
    public bool DrawInvalidatedByMembershipChange { get; private set; }

    /// <summary>Creates a new, empty group.</summary>
    /// <param name="name">A non-empty group name.</param>
    /// <returns>The created group.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    public static Group Create(string name) =>
        new(
            GuidV7.New(),
            NormalizeName(name),
            budget: null,
            eventDetails: null,
            members: [],
            exclusions: [],
            draws: [],
            drawInvalidatedByMembershipChange: false);

    /// <summary>Renames the group.</summary>
    /// <param name="name">A non-empty group name.</param>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Sets or clears the gift budget.</summary>
    /// <param name="budget">The budget, or <see langword="null"/> to clear it.</param>
    public void SetBudget(Budget? budget) => Budget = budget;

    /// <summary>Sets or clears the event details.</summary>
    /// <param name="eventDetails">The event details, or <see langword="null"/> to clear them.</param>
    public void SetEventDetails(EventDetails? eventDetails) => EventDetails = eventDetails;

    /// <summary>Adds a member to the group.</summary>
    /// <param name="name">A non-empty member name, unique within the group (case-insensitive).</param>
    /// <param name="wishlist">The member's wishlist.</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name duplicates an existing member.</exception>
    /// <remarks>Adding a member voids any existing draw; see <see cref="DrawInvalidatedByMembershipChange"/>.</remarks>
    public Guid AddMember(string name, Wishlist wishlist)
    {
        ArgumentNullException.ThrowIfNull(wishlist);

        var member = Member.Create(name, wishlist);
        GuardUniqueName(member.Name, excludingMemberId: null);
        _members.Add(member);
        InvalidateDraws();
        return member.Id;
    }

    /// <summary>
    /// Adds a member copied from a circle person, optionally linked to an account. Used only by the
    /// create-from-circle flow; the ordinary <see cref="AddMember"/> path stays account-free. The
    /// origin id is recorded and never dereferenced (FR-027).
    /// </summary>
    /// <param name="name">A non-empty, group-unique member name.</param>
    /// <param name="wishlist">The member's wishlist.</param>
    /// <param name="userId">The account to link the member to, or <see langword="null"/> to leave it unlinked.</param>
    /// <param name="originCirclePersonId">The circle person copied from, or <see langword="null"/>.</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the name duplicates an existing member, or when <paramref name="userId"/> is
    /// already linked to another member of this group.
    /// </exception>
    public Guid AddMemberFromCircle(string name, Wishlist wishlist, Guid? userId, Guid? originCirclePersonId)
    {
        ArgumentNullException.ThrowIfNull(wishlist);

        var member = Member.CreateFromCircle(name, wishlist, userId, originCirclePersonId);
        GuardUniqueName(member.Name, excludingMemberId: null);
        if (userId is { } linkedUser)
        {
            GuardUniqueUser(linkedUser, excludingMemberId: member.Id);
        }

        _members.Add(member);
        InvalidateDraws();
        return member.Id;
    }

    /// <summary>
    /// Links a member to an account. Linking changes how a member is <em>served</em> (their own
    /// assignment and wishlist ownership), not the participant set, so it does <strong>not</strong>
    /// invalidate a completed draw.
    /// </summary>
    /// <param name="memberId">The member to link.</param>
    /// <param name="userId">The account to link the member to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the member is unknown, or when <paramref name="userId"/> already backs another
    /// member of this group (one user backs at most one member per group).
    /// </exception>
    public void LinkMember(Guid memberId, Guid userId)
    {
        var member = RequireMember(memberId);
        GuardUniqueUser(userId, excludingMemberId: memberId);
        member.Link(userId);
    }

    /// <summary>
    /// Clears a member's account link, reverting it to unlinked. The member, its name and its
    /// wishlist survive; only how it is served changes. Does <strong>not</strong> invalidate a draw.
    /// </summary>
    /// <param name="memberId">The member to unlink.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown.</exception>
    public void UnlinkMember(Guid memberId) => RequireMember(memberId).Unlink();

    /// <summary>Edits an existing member's name and wishlist.</summary>
    /// <param name="memberId">The member to edit.</param>
    /// <param name="name">The new (non-empty, unique) name.</param>
    /// <param name="wishlist">The new wishlist.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown or the name duplicates another member.</exception>
    public void EditMember(Guid memberId, string name, Wishlist wishlist)
    {
        ArgumentNullException.ThrowIfNull(wishlist);

        var member = RequireMember(memberId);
        var normalized = NormalizeName(name);
        GuardUniqueName(normalized, excludingMemberId: memberId);
        member.Rename(normalized);
        member.SetWishlist(wishlist);
    }

    /// <summary>Removes a member and any exclusions that reference it.</summary>
    /// <param name="memberId">The member to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown.</exception>
    /// <remarks>Removing a member voids any existing draw; see <see cref="DrawInvalidatedByMembershipChange"/>.</remarks>
    public void RemoveMember(Guid memberId)
    {
        var member = RequireMember(memberId);
        _members.Remove(member);
        _exclusions.RemoveAll(e => e.GiverMemberId == memberId || e.RecipientMemberId == memberId);
        InvalidateDraws();
    }

    /// <summary>Adds a directed exclusion between two existing members.</summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <param name="recipientMemberId">The forbidden recipient member identity.</param>
    /// <exception cref="InvalidOperationException">Thrown when either endpoint is not a member of this group.</exception>
    public void AddExclusion(Guid giverMemberId, Guid recipientMemberId)
    {
        RequireMember(giverMemberId);
        RequireMember(recipientMemberId);

        var exclusion = Exclusion.Create(giverMemberId, recipientMemberId);
        if (!_exclusions.Contains(exclusion))
        {
            _exclusions.Add(exclusion);
        }
    }

    /// <summary>Removes a directed exclusion, if present.</summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <param name="recipientMemberId">The recipient member identity.</param>
    public void RemoveExclusion(Guid giverMemberId, Guid recipientMemberId) =>
        _exclusions.RemoveAll(e => e.GiverMemberId == giverMemberId && e.RecipientMemberId == recipientMemberId);

    /// <summary>Runs a new draw and, on success, appends it to the draw history as the current draw.</summary>
    /// <param name="generator">The draw generator domain service.</param>
    /// <param name="now">The current instant used to stamp the draw.</param>
    /// <returns>The <see cref="DrawResult"/> describing success or failure.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the group has fewer than two members.</exception>
    public DrawResult RunDraw(DrawGenerator generator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(generator);

        if (_members.Count < 2)
        {
            throw new InvalidOperationException("A draw needs at least two members.");
        }

        var result = generator.Generate(
            _members.Select(m => m.Id).ToList(),
            _exclusions,
            CurrentDraw?.ToMap());

        if (result.IsSuccess)
        {
            _draws.Add(Draw.Create(result.Assignments, now));
            DrawInvalidatedByMembershipChange = false;
        }

        return result;
    }

    /// <summary>Gets a member by identity, or <see langword="null"/> if not present.</summary>
    /// <param name="memberId">The member identity.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    public Member? FindMember(Guid memberId) => _members.FirstOrDefault(m => m.Id == memberId);

    /// <summary>Captures the group's full state as a serializable memento.</summary>
    /// <returns>The <see cref="GroupState"/> snapshot.</returns>
    public GroupState ToState() => new(
        Id,
        Name,
        Budget is null ? null : new BudgetState(Budget.Amount, Budget.Currency),
        EventDetails is null ? null : new EventDetailsState(EventDetails.Date, EventDetails.Location, EventDetails.Description),
        _members.Select(m => new MemberState(m.Id, m.Name, [.. m.Wishlist.Items], m.UserId, m.OriginCirclePersonId)).ToList(),
        _exclusions.Select(e => new ExclusionState(e.GiverMemberId, e.RecipientMemberId)).ToList(),
        _draws.Select(d => new DrawState(
            d.Id,
            d.CreatedAt,
            d.Assignments.Select(a => new AssignmentState(a.GiverMemberId, a.RecipientMemberId)).ToList())).ToList(),
        DrawInvalidatedByMembershipChange);

    /// <summary>Reconstructs a group from a previously captured memento.</summary>
    /// <param name="state">The state snapshot.</param>
    /// <returns>The rehydrated group.</returns>
    public static Group FromState(GroupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var members = state.Members
            .Select(m => Member.Restore(m.Id, m.Name, Wishlist.Create(m.Wishlist), m.UserId, m.OriginCirclePersonId))
            .ToList();

        var exclusions = state.Exclusions
            .Select(e => Exclusion.Create(e.GiverMemberId, e.RecipientMemberId))
            .ToList();

        var draws = state.Draws
            .Select(d => Draw.Restore(
                d.Id,
                d.CreatedAt,
                d.Assignments.Select(a => Assignment.Create(a.GiverMemberId, a.RecipientMemberId)).ToList()))
            .ToList();

        var budget = state.Budget is null ? null : Budget.Create(state.Budget.Amount, state.Budget.Currency);
        var eventDetails = state.Event is null
            ? null
            : EventDetails.Create(state.Event.Date, state.Event.Location, state.Event.Description);

        return new Group(
            state.Id,
            state.Name,
            budget,
            eventDetails,
            members,
            exclusions,
            draws,
            state.DrawInvalidatedByMembershipChange);
    }

    /// <summary>
    /// Discards the draw history because the member set changed. A draw maps a fixed set of givers
    /// to recipients, so once that set changes the draw — and every reveal link built from it — no
    /// longer describes the group. Clearing it here keeps <see cref="CurrentDraw"/> honest rather
    /// than leaving assignments that reference members who are no longer present.
    /// </summary>
    private void InvalidateDraws()
    {
        if (_draws.Count == 0)
        {
            return;
        }

        _draws.Clear();
        DrawInvalidatedByMembershipChange = true;
    }

    private Member RequireMember(Guid memberId) =>
        _members.FirstOrDefault(m => m.Id == memberId)
        ?? throw new InvalidOperationException($"Member '{memberId}' is not part of this group.");

    private void GuardUniqueUser(Guid userId, Guid excludingMemberId)
    {
        var duplicate = _members.Any(m => m.Id != excludingMemberId && m.UserId == userId);
        if (duplicate)
        {
            throw new InvalidOperationException(
                $"Account '{userId}' is already linked to another member of this group.");
        }
    }

    private void GuardUniqueName(string name, Guid? excludingMemberId)
    {
        var duplicate = _members.Any(m =>
            m.Id != excludingMemberId &&
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException($"A member named '{name}' already exists in this group.");
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name must not be empty.", nameof(name));
        }

        return name.Trim();
    }
}
