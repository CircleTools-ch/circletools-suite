using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.Core.Test.TestSupport;

/// <summary>Deterministic <see cref="IRandomSource"/> seeded with a fixed value for reproducible tests.</summary>
/// <param name="seed">The seed for the underlying pseudo-random generator.</param>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);

    /// <inheritdoc />
    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    /// <inheritdoc />
    public void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}

/// <summary>
/// Hand-written in-memory <see cref="IGroupRepository"/> that round-trips through
/// <see cref="GroupState"/>, mirroring the real (serializing) store.
/// </summary>
public sealed class InMemoryGroupRepository : IGroupRepository
{
    private readonly Dictionary<Guid, GroupState> _store = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Group>>(_store.Values.Select(Group.FromState).ToList());

    /// <inheritdoc />
    public Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(id, out var state) ? Group.FromState(state) : null);

    /// <inheritdoc />
    public Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        _store[group.Id] = group.ToState();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }
}
