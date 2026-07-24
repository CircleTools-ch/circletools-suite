using System.Text.Json;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Core.Persistence;

namespace AppSuite.SecretSanta.LocalStore;

/// <summary>
/// <see cref="IGroupRepository"/> that serializes the entire collection of groups (as
/// <see cref="GroupState"/> mementos) to JSON under a single key in an <see cref="IKeyValueStore"/>.
/// Suitable for the local-first MVP where the whole data set is small.
/// </summary>
/// <param name="store">The underlying key-value store.</param>
public sealed class GroupRepository(IKeyValueStore store) : IGroupRepository
{
    /// <summary>The storage key under which all groups are serialized.</summary>
    public const string StorageKey = "appsuite.secretsanta.groups";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Group>> ListAsync(CancellationToken cancellationToken)
    {
        var states = await LoadStatesAsync(cancellationToken);
        return states.Select(Group.FromState).ToList();
    }

    /// <inheritdoc />
    public async Task<Group?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var states = await LoadStatesAsync(cancellationToken);
        var state = states.FirstOrDefault(s => s.Id == id);
        return state is null ? null : Group.FromState(state);
    }

    /// <inheritdoc />
    public async Task SaveAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        var states = await LoadStatesAsync(cancellationToken);
        var index = states.FindIndex(s => s.Id == group.Id);
        var state = group.ToState();
        if (index >= 0)
        {
            states[index] = state;
        }
        else
        {
            states.Add(state);
        }

        await SaveStatesAsync(states, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var states = await LoadStatesAsync(cancellationToken);
        if (states.RemoveAll(s => s.Id == id) > 0)
        {
            await SaveStatesAsync(states, cancellationToken);
        }
    }

    private async Task<List<GroupState>> LoadStatesAsync(CancellationToken cancellationToken)
    {
        var json = await store.GetAsync(StorageKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var states = (List<GroupState>?)JsonSerializer.Deserialize(json, GroupStateJsonContext.Default.ListGroupState);
        return states ?? [];
    }

    private Task SaveStatesAsync(List<GroupState> states, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(states, GroupStateJsonContext.Default.ListGroupState);
        return store.SetAsync(StorageKey, json, cancellationToken);
    }
}
