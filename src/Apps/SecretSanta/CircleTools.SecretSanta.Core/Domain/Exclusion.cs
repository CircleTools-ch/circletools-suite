using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// An exclusion between two members of a group, owned by the <see cref="Group"/> aggregate root.
/// A <em>one-way</em> exclusion forbids only <c>giver → recipient</c>; a <em>mutual</em> exclusion
/// forbids both directions (neither member may be assigned the other), which is the usual choice for
/// a couple. For a mutual exclusion the endpoints are stored in a canonical order so that the same
/// pair is equal regardless of which member was picked first.
/// </summary>
public sealed class Exclusion : ValueObject
{
    private Exclusion(Guid giverMemberId, Guid recipientMemberId, bool mutual)
    {
        GiverMemberId = giverMemberId;
        RecipientMemberId = recipientMemberId;
        Mutual = mutual;
    }

    /// <summary>Gets the giver member identity (an endpoint of the pair when <see cref="Mutual"/>).</summary>
    public Guid GiverMemberId { get; }

    /// <summary>Gets the forbidden recipient member identity (the other endpoint when <see cref="Mutual"/>).</summary>
    public Guid RecipientMemberId { get; }

    /// <summary>Gets a value indicating whether the exclusion applies in both directions.</summary>
    public bool Mutual { get; }

    /// <summary>Creates an exclusion between two distinct members.</summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <param name="recipientMemberId">The forbidden recipient member identity.</param>
    /// <param name="mutual">
    /// <see langword="true"/> to forbid both directions (neither may be assigned the other);
    /// <see langword="false"/> (default) to forbid only <c>giver → recipient</c>.
    /// </param>
    /// <returns>The created exclusion.</returns>
    /// <exception cref="ArgumentException">Thrown when both endpoints are the same member.</exception>
    public static Exclusion Create(Guid giverMemberId, Guid recipientMemberId, bool mutual = false)
    {
        if (giverMemberId == recipientMemberId)
        {
            throw new ArgumentException("An exclusion must reference two different members.");
        }

        // Direction is meaningless for a mutual exclusion, so store the endpoints in a canonical order.
        // This makes "A ↔ B" and "B ↔ A" the same value and keeps deduplication order-independent.
        if (mutual && giverMemberId.CompareTo(recipientMemberId) > 0)
        {
            (giverMemberId, recipientMemberId) = (recipientMemberId, giverMemberId);
        }

        return new Exclusion(giverMemberId, recipientMemberId, mutual);
    }

    /// <summary>Indicates whether this exclusion forbids <paramref name="giver"/> → <paramref name="recipient"/>.</summary>
    /// <param name="giver">The prospective giver.</param>
    /// <param name="recipient">The prospective recipient.</param>
    /// <returns><see langword="true"/> when this exclusion forbids that assignment.</returns>
    public bool Forbids(Guid giver, Guid recipient) =>
        (GiverMemberId == giver && RecipientMemberId == recipient) ||
        (Mutual && GiverMemberId == recipient && RecipientMemberId == giver);

    /// <summary>Indicates whether this exclusion concerns the unordered pair {a, b}, ignoring direction.</summary>
    /// <param name="memberA">One member of the pair.</param>
    /// <param name="memberB">The other member of the pair.</param>
    /// <returns><see langword="true"/> when both members are the endpoints of this exclusion.</returns>
    public bool ConcernsPair(Guid memberA, Guid memberB) =>
        (GiverMemberId == memberA && RecipientMemberId == memberB) ||
        (GiverMemberId == memberB && RecipientMemberId == memberA);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GiverMemberId;
        yield return RecipientMemberId;
        yield return Mutual;
    }
}
