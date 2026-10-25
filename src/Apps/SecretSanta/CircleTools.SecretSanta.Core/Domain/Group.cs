using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// Aggregate root for a Secret-Santa group. Owns a <strong>shared</strong> roster (members) and shared
/// exclusions, plus zero or more <see cref="Event"/>s — the occasions that reuse that roster, each with
/// its own draw, wishlists, budget and event details (ADR-0042). Enforces the group's invariants:
/// non-empty name, unique member names, exclusion endpoints that are members, and at least two members
/// before a draw. A newly created group has <strong>no</strong> event — the user creates the first
/// occasion themselves (ADR-0042 amendment, 2026-09-01), so nothing is pre-named on their behalf.
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

    /// <summary>
    /// Gets the <strong>active</strong> roster — the members who take part in the group now. Archived
    /// members (removed but kept for history, ADR-0052) are excluded, so the roster editor, the draw and
    /// every "who is in this group" read see only current people. Name resolution for a past draw uses
    /// <see cref="FindMember"/>, which still finds archived members.
    /// </summary>
    public IReadOnlyList<Member> Members => [.. _members.Where(m => !m.IsArchived)];

    /// <summary>Gets the shared configured exclusions.</summary>
    public IReadOnlyList<Exclusion> Exclusions => _exclusions.AsReadOnly();

    /// <summary>Gets the group's events (occasions), oldest first. May be empty until the user adds one.</summary>
    public IReadOnlyList<Event> Events => _events.AsReadOnly();

    /// <summary>Creates a new, empty group with no members, no exclusions and no events.</summary>
    /// <param name="name">A non-empty group name.</param>
    /// <returns>The created group.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <remarks>
    /// A new group starts with <strong>no</strong> event (ADR-0042 amendment, 2026-09-01): the user
    /// creates the first occasion, so the app never pre-names one on their behalf.
    /// </remarks>
    public static Group Create(string name) =>
        new(GuidV7.New(), NormalizeName(name), members: [], exclusions: [], events: []);

    /// <summary>Renames the group.</summary>
    /// <param name="name">A non-empty group name.</param>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Adds a member to the shared roster.</summary>
    /// <param name="name">A non-empty member name, unique within the group (case-insensitive).</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name duplicates an existing member.</exception>
    /// <remarks>Adding a member flags every event's current draw as no longer matching the roster (it is
    /// kept, not discarded — ADR-0052); wishlists are set later, per event.</remarks>
    public Guid AddMember(string name)
    {
        var member = Member.Create(name);
        GuardUniqueName(member.Name, excludingMemberId: null);
        _members.Add(member);
        MarkRosterChangedOnAllEvents();
        return member.Id;
    }

    /// <summary>
    /// Adds a member copied from a circle person. Used only by the create-from-circle flow; the origin id is
    /// recorded and never dereferenced (FR-027).
    /// </summary>
    /// <param name="name">A non-empty, group-unique member name.</param>
    /// <param name="originCirclePersonId">The circle person copied from, or <see langword="null"/>.</param>
    /// <returns>The identity of the added member.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the name duplicates an existing member.</exception>
    public Guid AddMemberFromCircle(string name, Guid? originCirclePersonId)
    {
        var member = Member.CreateFromCircle(name, originCirclePersonId);
        GuardUniqueName(member.Name, excludingMemberId: null);
        _members.Add(member);
        MarkRosterChangedOnAllEvents();
        return member.Id;
    }

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

    /// <summary>
    /// Sets (or clears, when empty) a member's <see cref="Member.GiftNote"/> — the stable, per-person
    /// note their giver sees on the reveal (FR-037). Independent of renaming so neither clobbers the other.
    /// </summary>
    /// <param name="memberId">The member whose gift note is set.</param>
    /// <param name="giftNote">The gift note; empty or whitespace clears it.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown.</exception>
    /// <exception cref="ArgumentException">Thrown when the gift note exceeds <see cref="Member.MaxGiftNoteLength"/>.</exception>
    public void SetMemberGiftNote(Guid memberId, string? giftNote) =>
        RequireMember(memberId).SetGiftNote(giftNote);

    /// <summary>
    /// Removes a member from the active roster by <strong>archiving</strong> them (ADR-0052): the member
    /// is kept in the aggregate, so a past draw that includes them still resolves their name and a later
    /// rename still reaches them, but they take part in no future draw and are hidden from the roster.
    /// Exclusions that reference them are dropped (an archived member cannot be drawn), and their
    /// per-event wishlists are kept so a past occasion still reads correctly. Completed draws are
    /// <strong>not</strong> discarded; each event's current draw is flagged as no longer matching the
    /// roster so the UI can invite a re-draw.
    /// </summary>
    /// <param name="memberId">The active member to archive.</param>
    /// <exception cref="InvalidOperationException">Thrown when the member is unknown or already archived.</exception>
    public void RemoveMember(Guid memberId)
    {
        var member = RequireMember(memberId);
        member.Archive();
        _exclusions.RemoveAll(e => e.GiverMemberId == memberId || e.RecipientMemberId == memberId);
        MarkRosterChangedOnAllEvents();
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

    /// <summary>
    /// Adds a new event (occasion) by <strong>carrying forward</strong> from an existing one (FR-035): a
    /// <em>copy at creation</em>, not a live link (events stay independent, ADR-0042). Copies each
    /// <strong>current</strong> roster member's wishlist and the budget from the source event, and records
    /// the source event's current draw as the new event's rule-(c) baseline so its first draw does not
    /// reproduce the source's pairings. The new event is created <strong>undrawn</strong>. Exclusions are
    /// not copied — they are shared at the group level and already apply to every event.
    /// </summary>
    /// <param name="name">The new event name; surrounding whitespace is trimmed.</param>
    /// <param name="sourceEventId">The event to carry forward from.</param>
    /// <param name="now">The instant used to stamp the carried-forward draw baseline.</param>
    /// <param name="copyWishlists">Whether to copy the source event's per-member wishlists (default <see langword="true"/>).</param>
    /// <param name="copyBudget">Whether to copy the source event's budget (default <see langword="true"/>).</param>
    /// <param name="carryDrawBaseline">Whether to carry the source draw as the no-repeat baseline (default <see langword="true"/>).</param>
    /// <returns>The identity of the created event.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="sourceEventId"/> is not an event of this group.</exception>
    public Guid AddEventFrom(
        string name,
        Guid sourceEventId,
        DateTimeOffset now,
        bool copyWishlists = true,
        bool copyBudget = true,
        bool carryDrawBaseline = true)
    {
        var source = RequireEvent(sourceEventId);
        var occasion = Event.Create(GuidV7.New(), (name ?? string.Empty).Trim());

        if (copyBudget && source.Budget is not null)
        {
            occasion.SetBudget(source.Budget);
        }

        if (copyWishlists)
        {
            // Only current roster members carry a wishlist into the new occasion: a member archived since
            // the source occasion (ADR-0052) is history and does not take part in the new one.
            var activeIds = _members.Where(m => !m.IsArchived).Select(m => m.Id).ToHashSet();
            foreach (var (memberId, wishlist) in source.Wishlists)
            {
                if (activeIds.Contains(memberId))
                {
                    occasion.SetWishlist(memberId, wishlist);
                }
            }
        }

        if (carryDrawBaseline && source.CurrentDraw is { } prior)
        {
            occasion.SeedBaselineDraw(prior.Assignments, now);
        }

        _events.Add(occasion);
        return occasion.Id;
    }

    /// <summary>Renames an event.</summary>
    /// <param name="eventId">The event to rename.</param>
    /// <param name="name">The new name; may be empty.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event is unknown.</exception>
    public void RenameEvent(Guid eventId, string name) => RequireEvent(eventId).Rename(name);

    /// <summary>Removes an event and everything it owns (its draws and wishlists). A group may end up with none.</summary>
    /// <param name="eventId">The event to remove.</param>
    /// <exception cref="InvalidOperationException">Thrown when the event is unknown.</exception>
    public void RemoveEvent(Guid eventId) => _events.Remove(RequireEvent(eventId));

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
    /// <param name="ignoreCarriedBaseline">
    /// When <see langword="true"/>, ignore a draw baseline carried forward from a previous occasion
    /// (FR-035) for the no-repeat rule — the escape hatch when carrying forward over-constrains a small
    /// roster (UC-SS-014 5a). A real previous draw of this event is still honoured.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the group has fewer than two members, or the event is unknown.</exception>
    public DrawResult RunDraw(Guid eventId, DrawGenerator generator, DateTimeOffset now, bool ignoreCarriedBaseline = false)
    {
        ArgumentNullException.ThrowIfNull(generator);

        var occasion = RequireEvent(eventId);
        var active = _members.Where(m => !m.IsArchived).ToList();
        if (active.Count < 2)
        {
            throw new InvalidOperationException("A draw needs at least two members.");
        }

        var previous = ignoreCarriedBaseline
            ? occasion.CurrentDraw?.ToMap()
            : occasion.PreviousDrawForNoRepeat();

        var result = generator.Generate(
            active.Select(m => m.Id).ToList(),
            _exclusions,
            previous);

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
        _members.Select(m => new MemberState(m.Id, m.Name, OriginCirclePersonId: m.OriginCirclePersonId, IsArchived: m.IsArchived, GiftNote: m.GiftNote)).ToList(),
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
            .Select(m => Member.Restore(m.Id, m.Name, m.GiftNote, m.OriginCirclePersonId, m.IsArchived))
            .ToList();

        var exclusions = state.Exclusions
            .Select(e => Exclusion.Create(e.GiverMemberId, e.RecipientMemberId, e.Mutual))
            .ToList();

        // A pre-events snapshot has a null Events field and is migrated to a single default event; a
        // post-events snapshot carries Events explicitly, and an empty list is a real, intentional state
        // now that a new group starts with none (ADR-0042 amendment). Only null triggers migration.
        List<Event> events = state.Events is null
            ? [MigrateLegacyEvent(state)]
            : [.. state.Events.Select(FromEventState)];

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

        var budget = state.Budget is null ? null : Budget.Create(state.Budget.Amount, state.Budget.Currency, state.Budget.MinAmount);
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
        var budget = state.Budget is null ? null : Budget.Create(state.Budget.Amount, state.Budget.Currency, state.Budget.MinAmount);
        var details = state.EventDetails is null
            ? null
            : EventDetails.Create(state.EventDetails.Date, state.EventDetails.Location, state.EventDetails.Description);

        var baseline = state.CarriedForwardBaseline is { } b
            ? Draw.Restore(b.Id, b.CreatedAt, b.Assignments.Select(a => Assignment.Create(a.GiverMemberId, a.RecipientMemberId)).ToList())
            : null;

        return Event.Restore(state.Id, state.Name, budget, details, draws, wishlists, state.DrawInvalidatedByMembershipChange, baseline);
    }

    private static EventState ToEventState(Event occasion) => new(
        occasion.Id,
        occasion.Name,
        occasion.Budget is null ? null : new BudgetState(occasion.Budget.Amount, occasion.Budget.Currency, occasion.Budget.MinAmount),
        occasion.EventDetails is null
            ? null
            : new EventDetailsState(occasion.EventDetails.Date, occasion.EventDetails.Location, occasion.EventDetails.Description),
        occasion.Draws.Select(d => new DrawState(
            d.Id,
            d.CreatedAt,
            d.Assignments.Select(a => new AssignmentState(a.GiverMemberId, a.RecipientMemberId)).ToList())).ToList(),
        occasion.DrawInvalidatedByMembershipChange,
        occasion.Wishlists.Select(w => new EventWishlistState(w.Key, [.. w.Value.Items])).ToList(),
        occasion.BaselineDraw is { } baseline
            ? new DrawState(baseline.Id, baseline.CreatedAt, baseline.Assignments.Select(a => new AssignmentState(a.GiverMemberId, a.RecipientMemberId)).ToList())
            : null);

    private void MarkRosterChangedOnAllEvents()
    {
        foreach (var occasion in _events)
        {
            occasion.MarkRosterChanged();
        }
    }

    // Mutations act on the active roster: an archived member is history, not someone you rename, link or
    // re-archive. Name resolution for a past draw uses FindMember, which still finds archived members.
    private Member RequireMember(Guid memberId) =>
        _members.FirstOrDefault(m => m.Id == memberId && !m.IsArchived)
        ?? throw new InvalidOperationException($"Member '{memberId}' is not an active member of this group.");

    private Event RequireEvent(Guid eventId) =>
        _events.FirstOrDefault(e => e.Id == eventId)
        ?? throw new InvalidOperationException($"Event '{eventId}' is not part of this group.");

    // Uniqueness is enforced among active members only: an archived member's name is history and must not
    // block a new active member (e.g. re-adding someone you removed, ADR-0052).
    private void GuardUniqueName(string name, Guid? excludingMemberId)
    {
        var duplicate = _members.Any(m =>
            !m.IsArchived &&
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
