using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// A member of a Secret-Santa group — identity within the shared roster (a name, an optional account
/// link, an optional circle origin). What a member <em>wants</em> is not held here: wishlists are
/// per-<see cref="Event"/> (ADR-0042), because a member's list for one occasion is not their list for
/// another. What a member's giver should <em>know</em> — allergies, dietary needs, "please don't get
/// me…" (the <see cref="GiftNote"/>, FR-037) — <strong>is</strong> held here, because unlike a wishlist
/// it is a stable property of the person, the same for every occasion. Owned by the <see cref="Group"/>
/// aggregate root.
/// </summary>
public sealed class Member : Entity
{
    /// <summary>The maximum length of a <see cref="GiftNote"/>, bounding the encoded reveal link (ADR-0014).</summary>
    public const int MaxGiftNoteLength = 500;

    private Member(Guid id, string name, string giftNote, Guid? userId, Guid? originCirclePersonId, bool isArchived)
    {
        Id = id;
        Name = name;
        GiftNote = giftNote;
        UserId = userId;
        OriginCirclePersonId = originCirclePersonId;
        IsArchived = isArchived;
    }

    /// <summary>Gets the member's display name.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the member's <em>gift note</em> — free-text guidance the assigned giver sees on the reveal:
    /// allergies, dietary or lifestyle needs, and things <em>not</em> to buy (FR-037). Empty when unset.
    /// It is a stable attribute of the person (not per occasion, unlike a wishlist), so one note serves
    /// every event's draw. Distinct from an <see cref="Exclusion"/>, which constrains <em>who draws whom</em>.
    /// </summary>
    public string GiftNote { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this member has been <em>archived</em> — removed from the active
    /// roster but kept in the aggregate so past draws still resolve their name and a rename still reaches
    /// them (the "archive, don't delete" model, ADR-0052). An archived member takes part in no future
    /// draw and is hidden from the roster editor, but a completed draw that already includes them stays
    /// intact and viewable.
    /// </summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// Gets the account this member is linked to, or <see langword="null"/> when the member is not
    /// linked. The domain stores an <em>identifier, not a user</em>: it never navigates to Identity
    /// and never learns anything about the account behind the id (ADR-0019). Whether a linked member
    /// owns their wishlist or may see their assignment is resolved from this link state in the
    /// application layer, not represented as extra domain state.
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>Gets a value indicating whether this member is linked to an account.</summary>
    public bool IsLinked => UserId is not null;

    /// <summary>
    /// Gets the circle person this member was copied from when the group was created from a circle,
    /// or <see langword="null"/> otherwise. It is recorded at create time and <em>read
    /// defensively</em>: the domain never dereferences it into the circle, so a deleted circle can
    /// never make the group fail to open (FR-027, FR-022). To the domain it is an opaque
    /// <see cref="Guid"/> — the domain does not know it is a circle person.
    /// </summary>
    public Guid? OriginCirclePersonId { get; private set; }

    internal static Member Create(string name) =>
        new(GuidV7.New(), NormalizeName(name), giftNote: string.Empty, userId: null, originCirclePersonId: null, isArchived: false);

    internal static Member CreateFromCircle(string name, Guid? userId, Guid? originCirclePersonId) =>
        new(GuidV7.New(), NormalizeName(name), giftNote: string.Empty, userId, originCirclePersonId, isArchived: false);

    internal static Member Restore(Guid id, string name, string? giftNote, Guid? userId, Guid? originCirclePersonId, bool isArchived) =>
        new(id, name, NormalizeGiftNote(giftNote), userId, originCirclePersonId, isArchived);

    internal void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Sets (or clears, when empty) the member's <see cref="GiftNote"/>.</summary>
    internal void SetGiftNote(string? giftNote) => GiftNote = NormalizeGiftNote(giftNote);

    /// <summary>Archives the member: kept for history, but removed from the active roster (ADR-0052).</summary>
    internal void Archive() => IsArchived = true;

    internal void Link(Guid userId) => UserId = userId;

    internal void Unlink() => UserId = null;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Member name must not be empty.", nameof(name));
        }

        return name.Trim();
    }

    private static string NormalizeGiftNote(string? giftNote)
    {
        var trimmed = (giftNote ?? string.Empty).Trim();
        if (trimmed.Length > MaxGiftNoteLength)
        {
            throw new ArgumentException(
                $"Gift note must be at most {MaxGiftNoteLength} characters.", nameof(giftNote));
        }

        return trimmed;
    }
}
