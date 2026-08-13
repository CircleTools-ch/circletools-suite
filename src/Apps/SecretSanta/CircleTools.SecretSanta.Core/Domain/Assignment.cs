using CircleTools.SharedKernel;

namespace CircleTools.SecretSanta.Core.Domain;

/// <summary>A single giver-to-recipient assignment within a <see cref="Draw"/>.</summary>
public sealed class Assignment : ValueObject
{
    private Assignment(Guid giverMemberId, Guid recipientMemberId)
    {
        GiverMemberId = giverMemberId;
        RecipientMemberId = recipientMemberId;
    }

    /// <summary>Gets the giver member identity.</summary>
    public Guid GiverMemberId { get; }

    /// <summary>Gets the recipient member identity.</summary>
    public Guid RecipientMemberId { get; }

    internal static Assignment Create(Guid giverMemberId, Guid recipientMemberId)
    {
        if (giverMemberId == recipientMemberId)
        {
            throw new ArgumentException("A member cannot be assigned to themselves.");
        }

        return new Assignment(giverMemberId, recipientMemberId);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GiverMemberId;
        yield return RecipientMemberId;
    }
}
