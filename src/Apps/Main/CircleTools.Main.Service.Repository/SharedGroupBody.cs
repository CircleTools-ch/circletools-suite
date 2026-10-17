using System.Text.Json;
using System.Text.Json.Nodes;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// The few rules the shared-group store applies to a stored group body (ADR-0080): trimming a join-link
/// read to the proven member's own pairing, and merging a participant's own entry into the body.
/// </summary>
/// <remarks>
/// The store otherwise keeps the body opaque. These rules read only a narrow, named set of the
/// camel-cased Secret-Santa group memento's properties — <c>members</c> (<c>id</c>, <c>giftNote</c>,
/// <c>userId</c>, legacy <c>wishlist</c>), <c>draws</c> (legacy) and <c>events</c> (<c>id</c>,
/// <c>draws[].assignments[]</c>, <c>carriedForwardBaseline</c>, <c>wishlists[]</c>). Anything they don't
/// know is passed through untouched; a body without those properties is returned as is.
/// </remarks>
internal static class SharedGroupBody
{
    private const string Members = "members";
    private const string Events = "events";
    private const string Draws = "draws";
    private const string Assignments = "assignments";
    private const string Wishlists = "wishlists";
    private const string Id = "id";
    private const string MemberId = "memberId";
    private const string GiverMemberId = "giverMemberId";
    private const string RecipientMemberId = "recipientMemberId";
    private const string GiftNote = "giftNote";
    private const string UserId = "userId";
    private const string LegacyWishlist = "wishlist";
    private const string CarriedForwardBaseline = "carriedForwardBaseline";
    private const string Items = "items";

    /// <summary>
    /// Trims a body for a join-link reader (ADR-0080): of each event's draw history only the current draw
    /// stays, holding only <paramref name="member"/>'s assignments; the carried-forward baseline and legacy
    /// draws go; wishlists and gift notes stay only for the member and their recipient(s).
    /// </summary>
    /// <param name="body">The stored body.</param>
    /// <param name="member">The member the reader proved to be, or <see langword="null"/> for an unproven reader.</param>
    /// <returns>The trimmed body.</returns>
    public static JsonElement Redact(JsonElement body, Guid? member)
    {
        if (JsonNode.Parse(body.GetRawText()) is not JsonObject root)
        {
            return body;
        }

        // Legacy single-exchange draws (pre-events snapshots) are history nobody but the organizer needs.
        if (root[Draws] is JsonArray)
        {
            root[Draws] = new JsonArray();
        }

        var recipientsEverywhere = new HashSet<Guid>();
        if (root[Events] is JsonArray events)
        {
            foreach (var occasion in events.OfType<JsonObject>())
            {
                var recipients = TrimDraws(occasion, member);
                recipientsEverywhere.UnionWith(recipients);
                occasion.Remove(CarriedForwardBaseline);
                KeepWishlistsOf(occasion, member, recipients);
            }
        }

        if (root[Members] is JsonArray members)
        {
            foreach (var entry in members.OfType<JsonObject>())
            {
                var id = ReadGuid(entry, Id);
                var isMember = member is { } m && id == m;
                if (!isMember && (id is null || !recipientsEverywhere.Contains(id.Value)))
                {
                    entry.Remove(GiftNote);
                }

                if (!isMember)
                {
                    entry.Remove(UserId);
                    entry.Remove(LegacyWishlist);
                }
            }
        }

        return ToElement(root);
    }

    /// <summary>The members already linked to an account in the body (a <c>userId</c> is set).</summary>
    /// <param name="body">The stored body.</param>
    /// <returns>Each linked member with the account it is linked to.</returns>
    public static IReadOnlyList<(Guid MemberId, Guid AccountId)> LinkedMembers(JsonElement body)
    {
        var linked = new List<(Guid, Guid)>();
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty(Members, out var members)
            || members.ValueKind != JsonValueKind.Array)
        {
            return linked;
        }

        foreach (var entry in members.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object
                && entry.TryGetProperty(Id, out var id) && id.TryGetGuid(out var memberId)
                && entry.TryGetProperty(UserId, out var user) && user.ValueKind == JsonValueKind.String
                && user.TryGetGuid(out var accountId))
            {
                linked.Add((memberId, accountId));
            }
        }

        return linked;
    }

    /// <summary>
    /// Writes participants' own entries into a body (ADR-0080): each member's gift note and their wishlist
    /// for each event named in the entry. Members or events the body doesn't hold are skipped.
    /// </summary>
    /// <param name="body">The body to merge into.</param>
    /// <param name="entries">The recorded participant entries, keyed by member.</param>
    /// <returns>The merged body, and whether anything changed.</returns>
    public static (JsonElement Body, bool Changed) ApplyEntries(
        JsonElement body,
        IReadOnlyList<(Guid MemberId, ParticipantEntry Entry)> entries)
    {
        if (entries.Count == 0 || JsonNode.Parse(body.GetRawText()) is not JsonObject root)
        {
            return (body, false);
        }

        var before = root.ToJsonString();
        foreach (var (memberId, entry) in entries)
        {
            ApplyEntry(root, memberId, entry);
        }

        var changed = root.ToJsonString() != before;
        return (changed ? ToElement(root) : body, changed);
    }

    /// <summary>
    /// Reads a member's gift note and their wishlist for one event from a body (ADR-0084) — what the reveal
    /// page shows its holder for editing. Participant entries are already merged into a stored body, so this is
    /// the current value whoever wrote it.
    /// </summary>
    /// <param name="body">The stored body.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="eventId">The event.</param>
    /// <returns>The gift note (null when unset) and the wishlist items (empty when none).</returns>
    public static (string? GiftNote, IReadOnlyList<string> Items) ReadEntry(JsonElement body, Guid memberId, Guid eventId)
    {
        if (JsonNode.Parse(body.GetRawText()) is not JsonObject root)
        {
            return (null, []);
        }

        var member = (root[Members] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(m => ReadGuid(m, Id) == memberId);
        var giftNote = member?[GiftNote] is JsonValue note && note.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

        var occasion = (root[Events] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(e => ReadGuid(e, Id) == eventId);
        var wishlist = (occasion?[Wishlists] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(w => ReadGuid(w, MemberId) == memberId);
        var items = (wishlist?[Items] as JsonArray)?
            .Select(i => i is JsonValue v && v.TryGetValue<string>(out var item) ? item : null)
            .OfType<string>()
            .ToList() ?? [];

        return (giftNote, items);
    }

    // Keeps only the current (last) draw and, in it, only the member's assignments. Returns the
    // recipients the member gives to in this event.
    private static HashSet<Guid> TrimDraws(JsonObject occasion, Guid? member)
    {
        var recipients = new HashSet<Guid>();
        if (occasion[Draws] is not JsonArray draws || draws.Count == 0)
        {
            return recipients;
        }

        var last = draws[^1]?.DeepClone();
        draws.Clear();
        if (last is not JsonObject current)
        {
            return recipients;
        }

        var kept = new JsonArray();
        if (current[Assignments] is JsonArray assignments && member is { } m)
        {
            foreach (var assignment in assignments.OfType<JsonObject>())
            {
                if (ReadGuid(assignment, GiverMemberId) == m)
                {
                    kept.Add(assignment.DeepClone());
                    if (ReadGuid(assignment, RecipientMemberId) is { } recipient)
                    {
                        recipients.Add(recipient);
                    }
                }
            }
        }

        current[Assignments] = kept;
        draws.Add(current);
        return recipients;
    }

    private static void KeepWishlistsOf(JsonObject occasion, Guid? member, HashSet<Guid> recipients)
    {
        if (occasion[Wishlists] is not JsonArray wishlists)
        {
            return;
        }

        var kept = new JsonArray();
        foreach (var wishlist in wishlists.OfType<JsonObject>())
        {
            var owner = ReadGuid(wishlist, MemberId);
            if (owner is { } o && ((member is { } m && o == m) || recipients.Contains(o)))
            {
                kept.Add(wishlist.DeepClone());
            }
        }

        occasion[Wishlists] = kept;
    }

    private static void ApplyEntry(JsonObject root, Guid memberId, ParticipantEntry entry)
    {
        if (root[Members] is JsonArray members)
        {
            var target = members.OfType<JsonObject>().FirstOrDefault(m => ReadGuid(m, Id) == memberId);
            if (target is not null)
            {
                if (string.IsNullOrWhiteSpace(entry.GiftNote))
                {
                    target.Remove(GiftNote);
                }
                else
                {
                    target[GiftNote] = entry.GiftNote;
                }
            }
        }

        if (root[Events] is not JsonArray events)
        {
            return;
        }

        foreach (var wish in entry.Wishlists ?? [])
        {
            var occasion = events.OfType<JsonObject>().FirstOrDefault(e => ReadGuid(e, Id) == wish.EventId);
            if (occasion is null)
            {
                continue;
            }

            if (occasion[Wishlists] is not JsonArray wishlists)
            {
                wishlists = [];
                occasion[Wishlists] = wishlists;
            }

            for (var i = wishlists.Count - 1; i >= 0; i--)
            {
                if (wishlists[i] is JsonObject existing && ReadGuid(existing, MemberId) == memberId)
                {
                    wishlists.RemoveAt(i);
                }
            }

            var items = (wish.Items ?? []).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
            if (items.Count > 0)
            {
                var itemArray = new JsonArray();
                foreach (var item in items)
                {
                    itemArray.Add(item);
                }

                wishlists.Add(new JsonObject { [MemberId] = memberId.ToString(), [Items] = itemArray });
            }
        }
    }

    private static Guid? ReadGuid(JsonObject node, string property) =>
        node[property] is JsonValue value && value.TryGetValue<string>(out var text) && Guid.TryParse(text, out var id)
            ? id
            : null;

    private static JsonElement ToElement(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
