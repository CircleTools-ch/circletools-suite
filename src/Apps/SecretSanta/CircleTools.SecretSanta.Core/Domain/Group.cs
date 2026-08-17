using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Aggregate root for a Secret-Santa group. Owns a <strong>shared</strong> roster (members) and shared
/// exclusions, plus one or more <see cref="Event"/>s — the occasions that reuse that roster, each with
/// its own draw, wishlists, budget and event details (ADR-0042). Enforces the group's invariants:
/// non-empty name, unique member names, exclusion endpoints that are members, at least two members
/// before a draw, and at least one event at all times.
/// </summary>
public sealed class Group : AggregateRoot
{
    private readonly List<Member> _members;
    private readonly List<Exclusion> _exclusions;
    private readonly List<Event> _events;

    private Group(
        Guid id,
        string name,
        List<Member> members,
        List<Exclusion> exclusions,
        List<Event> events)
    {
        Id = id;
        Name = name;
        _members = members;
        _exclusions = exclusions;
        _events = events;
    }

    /// <summary>Gets the group name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the shared group members.</summary>
    public IReadOnlyList<Member> Members => _members.AsReadOnly();

    /// <summary>Gets the shared configured exclusions.</summary>
    public IReadOnlyList<Exclusion> Exclusions => _exclusions.AsReadOnly();

    /// <summary>Gets the group's events (occasions), oldest first. A group always has at least one.</summary>
    public IReadOnlyList<Event> Events => _events.AsReadOnly();

    /// <summary>Creates a new, empty group with a single default event whose id is the group's id.</summary>
    /// <param name="name">A non-empty group name.</param>
    /// <returns>The created group.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    public static Group Create(string name)
    {
        var id = GuidV7.New();

        // The first/default event takes the group's own id — a deterministic value that keeps the common
        // single-occasion group simple and lets the server migration line up existing rows (ADR-0042).
        return new(id, NormalizeName(name), members: [], exclusions: [], events: [Event.Create(id, string.Empty)]);
    }

    /// <summary>Renames the group.</summary>
    /// <param name="name">A non-empty group name.</param>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Adds a member to the shared roster.</summary>
    /// <param name="name">A non-empty member name, unique within the group (case-insensitive).</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name duplicates an existing member.</exception>
    /// <remarks>Adding a member voids every event's draw; wishlists are set later, per event.</remarks>
    public Guid AddMember(string name)
    {
        var member = Member.Create(name);
        GuardUniqueName(member.Name, excludingMemberId: null);
        _members.Add(member);
        InvalidateAllDraws();
        return member.Id;
    }

    /// <summary>
    /// Adds a member copied from a circle person, optionally linked to an account. Used only by the
    /// create-from-circle flow; the ordinary <see cref="AddMember"/> path stays account-free. The origin
    /// id is recorded and never dereferenced (FR-027).
    /// </summary>
    /// <param name="name">A non-empty, group-unique member name.</param>
    /// <param name="userId">The account to link the member to, or <see langword="null"/> to leave it unlinked.</param>
    /// <param name="originCirclePersonId">The circle person copied from, or <see langword="null"/>.</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the name duplicates an existing member, or when <paramref name="userId"/> is already
    /// linked to another member of this group.
    /// </exception>
    public Guid AddMemberFromCircle(string name, Guid? userId, Guid? originCirclePersonId)
    {
        var member = Member.CreateFromCircle(name, userId, originCirclePersonId);
        GuardUniqueName(member.Name, excludingMemberId: null);
        if (userId is { } linkedUser)
        {
            GuardUniqueUser(linkedUser, excludingMemberId: member.Id);
        }

        _members.Add(member);
        InvalidateAllDraws();
        return member.Id;
    }

    /// <summary>
    /// Links a member to an account. Linking changes how a member is <em>served</em> (their own
    /// assignment and wishlist ownership), not the participant set, so it does <strong>not</strong>
    /// invalidate any draw.
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

    /// <summary>Clears a member's account link, reverting it to unlinked. Does <strong>not</strong> invalidate a draw.</summary>
    /// <param name="memberId">The member to unlink.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown.</exception>
    public void UnlinkMember(Guid memberId) => RequireMember(memberId).Unlink();

    /// <summary>Edits an existing member's name. Wishlists are per event and edited there.</summary>
    /// <param name="memberId">The member to edit.</param>
    /// <param name="name">The new (non-empty, unique) name.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown or the name duplicates another member.</exception>
    public void EditMember(Guid memberId, string name)
    {
        var member = RequireMember(memberId);
        var normalized = NormalizeName(name);
        GuardUniqueName(normalized, excludingMemberId: memberId);
        member.Rename(normalized);
    }

    /// <summary>Removes a member, any exclusions that reference it, and its wishlist in every event.</summary>
    /// <param name="memberId">The member to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown.</exception>
    /// <remarks>Removing a member voids every event's draw.</remarks>
    public void RemoveMember(Guid memberId)
    {
        var member = RequireMember(memberId);
        _members.Remove(member);
        _exclusions.RemoveAll(e => e.GiverMemberId == memberId || e.RecipientMemberId == memberId);
        foreach (var occasion in _events)
        {
            occasion.RemoveMember(memberId);
        }

        InvalidateAllDraws();
    }

    /// <summary>
    /// Adds an exclusion between two existing members (shared across events). A member pair carries a
    /// single rule, so this replaces any existing exclusion for the same pair — switching a one-way
    /// rule to mutual, or vice versa.
    /// </summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <param name="recipientMemberId">The forbidden recipient member identity.</param>
    /// <param name="mutual">
    /// <see langword="true"/> to forbid both directions (the couple case); <see langword="false"/>
    /// (default) to forbid only <c>giver → recipient</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when either endpoint is not a member of this group.</exception>
    public void AddExclusion(Guid giverMemberId, Guid recipientMemberId, bool mutual = false)
    {
        RequireMember(giverMemberId);
        RequireMember(recipientMemberId);

        var exclusion = Exclusion.Create(giverMemberId, recipientMemberId, mutual);
        _exclusions.RemoveAll(e => e.ConcernsPair(giverMemberId, recipientMemberId));
        _exclusions.Add(exclusion);
    }

    /// <summary>Removes the exclusion for the given member pair, if present (in either direction).</summary>
    /// <param name="giverMemberId">One endpoint of the pair.</param>
    /// <param name="recipientMemberId">The other endpoint of the pair.</param>
    public void RemoveExclusion(Guid giverMemberId, Guid recipientMemberId) =>
        _exclusions.RemoveAll(e => e.ConcernsPair(giverMemberId, recipientMemberId));

    /// <summary>Adds a new event (occasion) to the group. The name is trimmed; may end up empty.</summary>
    /// <param name="name">The event name; surrounding whitespace is trimmed.</param>
    /// <returns>The identity of the created event.</returns>
    public Guid AddEvent(string name)
    {
        var occasion = Event.Create(GuidV7.New(), (name ?? string.Empty).Trim());
        _events.Add(occasion);
        return occasion.Id;
    }

    /// <summary>Renames an event.</summary>
    /// <param name="eventId">The event to rename.</param>
    /// <param name="name">The new name; may be empty.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event is unknown.</exception>
    public void RenameEvent(Guid eventId, string name) => RequireEvent(eventId).Rename(name);

    /// <summary>Removes an event and everything it owns (its draws and wishlists).</summary>
    /// <param name="eventId">The event to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event is unknown or it is the group's last event.</exception>
    public void RemoveEvent(Guid eventId)
    {
        var occasion = RequireEvent(eventId);
        if (_events.Count == 1)
        {
            throw new InvalidOperationException("A group must always have at least one event.");
        }

        _events.Remove(occasion);
    }

    /// <summary>Sets or clears an event's gift budget.</summary>
    public void SetEventBudget(Guid eventId, Budget? budget) => RequireEvent(eventId).SetBudget(budget);

    /// <summary>Sets or clears an event's details (date, location, description).</summary>
    public void SetEventDetails(Guid eventId, EventDetails? eventDetails) =>
        RequireEvent(eventId).SetEventDetails(eventDetails);

    /// <summary>Sets (or clears, when empty) a member's wishlist for one event.</summary>
    /// <param name="eventId">The event the wishlist belongs to.</param>
    /// <param name="memberId">The member whose wishlist it is.</param>
    /// <param name="wishlist">The wishlist.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event or member is unknown.</exception>
    public void SetMemberWishlist(Guid eventId, Guid memberId, Wishlist wishlist)
    {
        ArgumentNullException.ThrowIfNull(wishlist);
        RequireMember(memberId);
        RequireEvent(eventId).SetWishlist(memberId, wishlist);
    }

    /// <summary>Runs a new draw for one event and, on success, appends it to that event's history.</summary>
    /// <param name="eventId">The event to draw.</param>
    /// <param name="generator">The draw generator domain service.</param>
    /// <param name="now">The current instant used to stamp the draw.</param>
    /// <returns>The <see cref="DrawResult"/> describing success or failure.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the group has fewer than two members, or the event is unknown.</exception>
    public DrawResult RunDraw(Guid eventId, DrawGenerator generator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(generator);

        var occasion = RequireEvent(eventId);
        if (_members.Count < 2)
        {
            throw new InvalidOperationException("A draw needs at least two members.");
        }

        var result = generator.Generate(
            _members.Select(m => m.Id).ToList(),
            _exclusions,
            occasion.CurrentDraw?.ToMap());

        if (result.IsSuccess)
        {
            occasion.AppendDraw(result.Assignments, now);
        }

        return result;
    }

    /// <summary>Gets a member by identity, or <see langword="null"/> if not present.</summary>
    /// <param name="memberId">The member identity.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    public Member? FindMember(Guid memberId) => _members.FirstOrDefault(m => m.Id == memberId);

    /// <summary>Gets an event by identity, or <see langword="null"/> if not present.</summary>
    /// <param name="eventId">The event identity.</param>
    /// <returns>The event, or <see langword="null"/>.</returns>
    public Event? FindEvent(Guid eventId) => _events.FirstOrDefault(e => e.Id == eventId);

    /// <summary>Captures the group's full state as a serializable memento.</summary>
    /// <returns>The <see cref="GroupState"/> snapshot.</returns>
    public GroupState ToState() => new(
        Id,
        Name,
        // Legacy single-exchange fields are no longer written; they exist only so an older snapshot can
        // still be read and migrated (see FromState). New writes carry everything under Events.
        Budget: null,
        Event: null,
        _members.Select(m => new MemberState(m.Id, m.Name, UserId: m.UserId, OriginCirclePersonId: m.OriginCirclePersonId)).ToList(),
        _exclusions.Select(e => new ExclusionState(e.GiverMemberId, e.RecipientMemberId, e.Mutual)).ToList(),
        Draws: [],
        DrawInvalidatedByMembershipChange: false,
        Events: _events.Select(ToEventState).ToList());

    /// <summary>Reconstructs a group from a previously captured memento, migrating a pre-events snapshot in place.</summary>
    /// <param name="state">The state snapshot.</param>
    /// <returns>The rehydrated group.</returns>
    public static Group FromState(GroupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var members = state.Members
            .Select(m => Member.Restore(m.Id, m.Name, m.UserId, m.OriginCirclePersonId))
            .ToList();

        var exclusions = state.Exclusions
            .Select(e => Exclusion.Create(e.GiverMemberId, e.RecipientMemberId, e.Mutual))
            .ToList();

        var events = state.Events is { Count: > 0 } storedEvents
            ? storedEvents.Select(FromEventState).ToList()
            : [MigrateLegacyEvent(state)];

        return new Group(state.Id, state.Name, members, exclusions, events);
    }

    // A snapshot written before events existed carried a single budget, event details, draw history and
    // per-member wishlist. Lift them into one default event whose id is the group id — the same
    // deterministic value the server-side linking migration uses to re-key existing rows (ADR-0042).
    private static Event MigrateLegacyEvent(GroupState state)
    {
        var draws = state.Draws
            .Select(d => Draw.Restore(
                d.Id, d.CreatedAt, d.Assignments.Select(a => Assignment.Create(a.GiverMemberId, a.RecipientMemberId)).ToList()))
            .ToList();

        var wishlists = state.Members
            .Where(m => m.Wishlist is { Count: > 0 })
            .ToDictionary(m => m.Id, m => Wishlist.Create(m.Wishlist));

        var budget = state.Budget is null ? null : Budget.Create(state.Budget.Amount, state.Budget.Currency);
        var details = state.Event is null
            ? null
            : EventDetails.Create(state.Event.Date, state.Event.Location, state.Event.Description);

        return Event.Restore(state.Id, string.Empty, budget, details, draws, wishlists, state.DrawInvalidatedByMembershipChange);
    }

    private static Event FromEventState(EventState state)
    {
        var draws = state.Draws
            .Select(d => Draw.Restore(
                d.Id, d.CreatedAt, d.Assignments.Select(a => Assignment.Create(a.GiverMemberId, a.RecipientMemberId)).ToList()))
            .ToList();

        var wishlists = state.Wishlists.ToDictionary(w => w.MemberId, w => Wishlist.Create(w.Items));
        var budget = state.Budget is null ? null : Budget.Create(state.Budget.Amount, state.Budget.Currency);
        var details = state.EventDetails is null
            ? null
            : EventDetails.Create(state.EventDetails.Date, state.EventDetails.Location, state.EventDetails.Description);

        return Event.Restore(state.Id, state.Name, budget, details, draws, wishlists, state.DrawInvalidatedByMembershipChange);
    }

    private static EventState ToEventState(Event occasion) => new(
        occasion.Id,
        occasion.Name,
        occasion.Budget is null ? null : new BudgetState(occasion.Budget.Amount, occasion.Budget.Currency),
        occasion.EventDetails is null
            ? null
            : new EventDetailsState(occasion.EventDetails.Date, occasion.EventDetails.Location, occasion.EventDetails.Description),
        occasion.Draws.Select(d => new DrawState(
            d.Id,
            d.CreatedAt,
            d.Assignments.Select(a => new AssignmentState(a.GiverMemberId, a.RecipientMemberId)).ToList())).ToList(),
        occasion.DrawInvalidatedByMembershipChange,
        occasion.Wishlists.Select(w => new EventWishlistState(w.Key, [.. w.Value.Items])).ToList());

    private void InvalidateAllDraws()
    {
        foreach (var occasion in _events)
        {
            occasion.InvalidateDraws();
        }
    }

    private Member RequireMember(Guid memberId) =>
        _members.FirstOrDefault(m => m.Id == memberId)
        ?? throw new InvalidOperationException($"Member '{memberId}' is not part of this group.");

    private Event RequireEvent(Guid eventId) =>
        _events.FirstOrDefault(e => e.Id == eventId)
        ?? throw new InvalidOperationException($"Event '{eventId}' is not part of this group.");

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
            throw new DuplicateMemberNameException(name);
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
