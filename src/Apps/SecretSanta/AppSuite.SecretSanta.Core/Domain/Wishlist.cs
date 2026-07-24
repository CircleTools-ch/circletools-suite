using AppSuite.SharedKernel;

namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>Value object holding an ordered list of a member's wishlist items.</summary>
public sealed class Wishlist : ValueObject
{
    private Wishlist(IReadOnlyList<string> items)
    {
        Items = items;
    }

    /// <summary>Gets an empty wishlist.</summary>
    public static Wishlist Empty { get; } = new([]);

    /// <summary>Gets the wishlist items in order.</summary>
    public IReadOnlyList<string> Items { get; }

    /// <summary>Creates a <see cref="Wishlist"/> from the supplied items, dropping empty entries.</summary>
    /// <param name="items">The candidate items; null or whitespace entries are ignored.</param>
    /// <returns>The created wishlist.</returns>
    public static Wishlist Create(IEnumerable<string>? items)
    {
        if (items is null)
        {
            return Empty;
        }

        var cleaned = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

        return cleaned.Count == 0 ? Empty : new Wishlist(cleaned);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() => Items;
}
