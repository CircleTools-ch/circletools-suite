using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.LocalStore.Test.TestSupport;

/// <summary>Builders for sync-engine tests: group mementos, and the wire bodies a pull carries.</summary>
public static class SyncTestData
{
    /// <summary>Builds a group memento with the given name and members.</summary>
    /// <param name="name">The group name.</param>
    /// <param name="memberNames">The member names to add.</param>
    /// <returns>The group's serializable memento.</returns>
    public static GroupState GroupWith(string name, params string[] memberNames)
    {
        var group = Group.Create(name);
        foreach (var memberName in memberNames)
        {
            group.AddMember(memberName, Wishlist.Empty);
        }

        return group.ToState();
    }

    /// <summary>Serializes a memento to the opaque JSON body the sync wire carries.</summary>
    /// <param name="state">The memento to serialize.</param>
    /// <returns>The body element.</returns>
    public static JsonElement Body(GroupState state) =>
        JsonSerializer.SerializeToElement(state, SyncJsonContext.Default.GroupState);

    /// <summary>A pulled group change (kind <c>group</c>) carrying <paramref name="state"/> at a version.</summary>
    /// <param name="state">The group memento.</param>
    /// <param name="version">The aggregate version.</param>
    /// <returns>The change.</returns>
    public static SyncChange GroupChange(GroupState state, long version) =>
        new(SyncKinds.Group, state.Id, version, Body(state));
}
