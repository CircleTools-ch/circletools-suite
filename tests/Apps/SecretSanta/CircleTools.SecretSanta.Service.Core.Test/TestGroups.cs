// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.Core.Persistence;

namespace CircleTools.SecretSanta.Service.Core.Test;

/// <summary>Builds group documents through the real domain, as the apps do.</summary>
internal static class TestGroups
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A group of four with one event, drawn, and a wishlist for Ben.</summary>
    public static (GroupState Document, Guid EventId, IReadOnlyDictionary<string, Guid> Members) Drawn()
    {
        var group = Group.Create("Family");
        var members = new Dictionary<string, Guid>();
        foreach (var name in new[] { "Anna", "Ben", "Cleo", "Dan" })
        {
            members[name] = group.AddMember(name);
        }

        var eventId = group.AddEvent("Christmas 2026");
        group.SetMemberWishlist(eventId, members["Ben"], Wishlist.Create(["Scarf", "Tea"]));
        var result = group.RunDraw(eventId, new DrawGenerator(new SystemRandomSource()), Now);
        Assert.True(result.IsSuccess);

        return (group.ToState(), eventId, members);
    }

    /// <summary>A group with members and an event but no draw yet.</summary>
    public static (GroupState Document, Guid EventId, Guid MemberId) Undrawn()
    {
        var group = Group.Create("Friends");
        var memberId = group.AddMember("Anna");
        group.AddMember("Ben");
        var eventId = group.AddEvent("Wichteln");
        return (group.ToState(), eventId, memberId);
    }

    /// <summary>Whom <paramref name="giver"/> gives to in the document's current draw of the event.</summary>
    public static Guid RecipientOf(GroupState document, Guid eventId, Guid giver) =>
        Group.FromState(document).FindEvent(eventId)!.CurrentDraw!.Assignments.Single(a => a.GiverMemberId == giver).RecipientMemberId;

    /// <summary>A member's name.</summary>
    public static string NameOf(GroupState document, Guid memberId) =>
        Group.FromState(document).FindMember(memberId)!.Name;
}
