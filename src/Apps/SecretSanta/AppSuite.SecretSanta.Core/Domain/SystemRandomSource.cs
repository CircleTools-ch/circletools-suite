namespace AppSuite.SecretSanta.Core.Domain;

/// <summary>Production <see cref="IRandomSource"/> backed by <see cref="Random.Shared"/>.</summary>
public sealed class SystemRandomSource : IRandomSource
{
    /// <inheritdoc />
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);

    /// <inheritdoc />
    public void Shuffle<T>(IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
