using System.Diagnostics.CodeAnalysis;
using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// A single gift-exchange occasion within a <see cref="Group"/> (ADR-0042). Occasions reuse the group's
/// shared roster and exclusions, but each one owns its own draw history, its own per-member wishlists,
/// and its own budget and event details — so re-drawing one occasion never disturbs another, and a
/// birthday wishlist is not a Christmas wishlist. Owned by the <see cref="Group"/> aggregate root; all
/// mutators are internal so the aggregate stays the only writer.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Event' is the ubiquitous-language domain term (glossary/event.md, ADR-0042); the suite is C#-only, so the VB.NET keyword clash does not apply.")]
public sealed class Event : Entity
{
    private readonly List<Draw> _draws;
    private readonly Dictionary<Guid, Wishlist> _wishlists;
    private Draw? _baselineDraw;

    private Event(
        Guid id,
        string name,
        Budget? budget,
        EventDetails? eventDetails,
        List<Draw> draws,
        Dictionary<Guid, Wishlist> wishlists,
        bool drawInvalidatedByMembershipChange,
        Draw? baselineDraw)
    {
        Id = id;
        Name = name;
        Budget = budget;
        EventDetails = eventDetails;
        _draws = draws;
        _wishlists = wishlists;
        DrawInvalidatedByMembershipChange = drawInvalidatedByMembershipChange;
        _baselineDraw = baselineDraw;
    }

    /// <summary>
    /// Gets the event's name. May be empty — a group's first/only event commonly has none, and the UI
    /// renders an empty name as a generic localized label. A user creating a second occasion names it.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>Gets this event's optional gift budget.</summary>
    public Budget? Budget { get; private set; }

    /// <summary>Gets this event's optional event details (date, location, description).</summary>
    public EventDetails? EventDetails { get; private set; }

    /// <summary>Gets this event's ordered draw history (oldest first).</summary>
    public IReadOnlyList<Draw> Draws => _draws.AsReadOnly();

    /// <summary>Gets this event's current (most recent) draw, or <see langword="null"/> if none.</summary>
    public Draw? CurrentDraw => _draws.Count > 0 ? _draws[^1] : null;

    /// <summary>
    /// Gets a value indicating whether the shared roster changed after this event's current draw was
    /// produced, so the draw no longer matches it. Under the "archive, don't delete" model (ADR-0052)
    /// the draw is <strong>kept</strong> as history — this is a <em>stale</em> flag, not a deletion — so
    /// the UI can show the completed draw while inviting a re-draw that includes (or drops) whoever
    /// changed. A fresh draw for the current roster clears it.
    /// </summary>
    public bool DrawInvalidatedByMembershipChange { get; private set; }

    /// <summary>Gets the member ids that have a non-empty wishlist for this event.</summary>
    public IReadOnlyCollection<Guid> WishlistMemberIds => _wishlists.Keys;

    /// <summary>
    /// Gets the <em>baseline</em> draw carried forward from a previous occasion (FR-035), or
    /// <see langword="null"/>. It is <strong>not</strong> part of <see cref="Draws"/> and never becomes
    /// <see cref="CurrentDraw"/> — a carried-forward event reads as <em>undrawn</em> (no reveal links) —
    /// but it is honoured as the "previous draw" for draw rule (c) so the event's <em>first</em> draw does
    /// not reproduce last time's pairings. Once a real draw is appended it is cleared, since
    /// <see cref="CurrentDraw"/> then supplies the rule-(c) baseline.
    /// </summary>
    internal Draw? BaselineDraw => _baselineDraw;

    /// <summary>
    /// Creates a new, empty event with no budget, event details, wishlists or draws. The id is supplied
    /// by the <see cref="Group"/>: the group's first/default event takes the group's own id (a
    /// deterministic value the server-side migration can compute without coordination, ADR-0042), and a
    /// later user-added event takes a fresh <see cref="GuidV7"/>.
    /// </summary>
    /// <param name="id">The event identity.</param>
    /// <param name="name">The event name; may be empty.</param>
    /// <returns>The created event.</returns>
    internal static Event Create(Guid id, string name) =>
        new(id, (name ?? string.Empty).Trim(), budget: null, eventDetails: null, draws: [], wishlists: [], drawInvalidatedByMembershipChange: false, baselineDraw: null);

    /// <summary>Reconstructs an event from a previously captured memento.</summary>
    internal static Event Restore(
        Guid id,
        string name,
        Budget? budget,
        EventDetails? eventDetails,
        List<Draw> draws,
        Dictionary<Guid, Wishlist> wishlists,
        bool drawInvalidatedByMembershipChange,
        Draw? baselineDraw = null) =>
        new(id, (name ?? string.Empty).Trim(), budget, eventDetails, draws, wishlists, drawInvalidatedByMembershipChange, baselineDraw);

    /// <summary>Gets the wishlist a member has for this event, or an empty one if they have set none.</summary>
    /// <param name="memberId">The member whose wishlist is wanted.</param>
    /// <returns>The member's wishlist for this event, or <see cref="Wishlist.Empty"/>.</returns>
    public Wishlist WishlistFor(Guid memberId) =>
        _wishlists.TryGetValue(memberId, out var wishlist) ? wishlist : Wishlist.Empty;

    internal void Rename(string name) => Name = (name ?? string.Empty).Trim();

    internal void SetBudget(Budget? budget) => Budget = budget;

    internal void SetEventDetails(EventDetails? eventDetails) => EventDetails = eventDetails;

    /// <summary>Sets (or clears, when empty) a member's wishlist for this event. Does not invalidate the draw.</summary>
    internal void SetWishlist(Guid memberId, Wishlist wishlist)
    {
        ArgumentNullException.ThrowIfNull(wishlist);
        if (wishlist.Items.Count == 0)
        {
            _wishlists.Remove(memberId);
        }
        else
        {
            _wishlists[memberId] = wishlist;
        }
    }

    /// <summary>Appends a successful draw and clears the roster-changed flag (the draw now matches the roster).</summary>
    internal void AppendDraw(IReadOnlyList<Assignment> assignments, DateTimeOffset now)
    {
        _draws.Add(Draw.Create(assignments, now));
        DrawInvalidatedByMembershipChange = false;

        // A real draw now supplies the rule-(c) baseline; the carried-forward one has served its purpose.
        _baselineDraw = null;
    }

    /// <summary>
    /// Records a draw carried forward from a previous occasion as this event's rule-(c)
    /// <see cref="BaselineDraw"/> (FR-035). A no-op once the event has any real draw of its own — the
    /// event's own <see cref="CurrentDraw"/> is then the correct baseline. The carried assignments are not
    /// added to <see cref="Draws"/>, so the event stays undrawn.
    /// </summary>
    /// <param name="assignments">The previous occasion's assignments to carry as the baseline.</param>
    /// <param name="now">The instant used to stamp the baseline draw.</param>
    internal void SeedBaselineDraw(IReadOnlyList<Assignment> assignments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (_draws.Count > 0 || assignments.Count == 0)
        {
            return;
        }

        _baselineDraw = Draw.Create(assignments, now);
    }

    /// <summary>
    /// Gets the giver→recipient map the next draw must not reproduce (rule (c)): the event's own
    /// <see cref="CurrentDraw"/> once it has one, else the carried-forward <see cref="BaselineDraw"/>, else
    /// <see langword="null"/>.
    /// </summary>
    internal IReadOnlyDictionary<Guid, Guid>? PreviousDrawForNoRepeat() => (CurrentDraw ?? _baselineDraw)?.ToMap();

    /// <summary>
    /// Flags this event's current draw as no longer matching the shared roster, because a member was
    /// added or archived. The draw is <strong>kept</strong> (ADR-0052: draws are permanent history) — a
    /// completed draw is a real past result the organizer may still want to see — so this only sets the
    /// stale flag; a re-draw clears it. A no-op when there is no draw to flag.
    /// </summary>
    internal void MarkRosterChanged()
    {
        if (_draws.Count == 0)
        {
            return;
        }

        DrawInvalidatedByMembershipChange = true;
    }

    /// <summary>Enumerates this event's per-member wishlists as (memberId, wishlist) pairs, for the memento.</summary>
    internal IEnumerable<KeyValuePair<Guid, Wishlist>> Wishlists => _wishlists;
}
