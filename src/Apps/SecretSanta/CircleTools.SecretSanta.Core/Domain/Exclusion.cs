using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>
/// A directed exclusion within a group: the giver must not be assigned the recipient.
/// Owned by the <see cref="Group"/> aggregate root.
/// </summary>
public sealed class Exclusion : ValueObject
{
    private Exclusion(Guid giverMemberId, Guid recipientMemberId)
    {
        GiverMemberId = giverMemberId;
        RecipientMemberId = recipientMemberId;
    }

    /// <summary>Gets the giver member identity.</summary>
    public Guid GiverMemberId { get; }

    /// <summary>Gets the forbidden recipient member identity.</summary>
    public Guid RecipientMemberId { get; }

    /// <summary>Creates a directed exclusion between two distinct members.</summary>
    /// <param name="giverMemberId">The giver member identity.</param>
    /// <param name="recipientMemberId">The forbidden recipient member identity.</param>
    /// <returns>The created exclusion.</returns>
    /// <exception cref="ArgumentException">Thrown when both endpoints are the same member.</exception>
    public static Exclusion Create(Guid giverMemberId, Guid recipientMemberId)
    {
        if (giverMemberId == recipientMemberId)
        {
            throw new ArgumentException("An exclusion must reference two different members.");
        }

        return new Exclusion(giverMemberId, recipientMemberId);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GiverMemberId;
        yield return RecipientMemberId;
    }
}
