using AppSuite.SharedKernel;

namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>A member of a Secret-Santa group. Owned by the <see cref="Group"/> aggregate root.</summary>
public sealed class Member : Entity
{
    private Member(Guid id, string name, Wishlist wishlist)
    {
        Id = id;
        Name = name;
        Wishlist = wishlist;
    }

    /// <summary>Gets the member's display name.</summary>
    public string Name { get; private set; }

    /// <summary>Gets the member's wishlist.</summary>
    public Wishlist Wishlist { get; private set; }

    internal static Member Create(string name, Wishlist wishlist) =>
        new(GuidV7.New(), NormalizeName(name), wishlist);

    internal static Member Restore(Guid id, string name, Wishlist wishlist) =>
        new(id, name, wishlist);

    internal void Rename(string name) => Name = NormalizeName(name);

    internal void SetWishlist(Wishlist wishlist) => Wishlist = wishlist;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Member name must not be empty.", nameof(name));
        }

        return name.Trim();
    }
}
