using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Entries;

namespace CircleTools.SecretSanta.Service.Core.Groups;

/// <summary>An online group's one server copy (ADR-0086): the document, with member entries merged in.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="Version">The document version, raised on every change.</param>
/// <param name="SchemaVersion">The document schema version of the last write.</param>
/// <param name="Document">The document.</param>
/// <param name="JoinEnabled">Whether the join link lets people in.</param>
/// <param name="UpdatedAt">When the document last changed.</param>
/// <param name="UpdatedBy">The role assignment that last wrote the document, if known.</param>
public sealed record OnlineGroup(
    Guid GroupId,
    long Version,
    int SchemaVersion,
    GroupState Document,
    bool JoinEnabled,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy);

/// <summary>Everything the organizer reads about an online group.</summary>
/// <param name="Group">The group.</param>
/// <param name="Assignments">Who has a role in it.</param>
/// <param name="Entries">The members who keep their own entry.</param>
public sealed record OnlineGroupDetails(OnlineGroup Group, IReadOnlyList<RoleAssignment> Assignments, IReadOnlyList<MemberEntry> Entries);

/// <summary>
/// How a request proves who it is (ADR-0086): a signed-in account, the organizer link, or a device key. A request
/// may carry several; the store resolves the one that holds a role in the group.
/// </summary>
/// <param name="AccountId">The signed-in account, if any.</param>
/// <param name="OrganizerToken">The organizer link's token, if presented.</param>
/// <param name="DeviceKey">The device key, if presented.</param>
public sealed record Caller(Guid? AccountId, string? OrganizerToken, string? DeviceKey)
{
    /// <summary>Gets a caller that proves nothing.</summary>
    public static Caller Anonymous { get; } = new(null, null, null);
}

/// <summary>One giver's reveal token in one event.</summary>
/// <param name="EventId">The event.</param>
/// <param name="GiverMemberId">The giver.</param>
/// <param name="Token">The token (the secret in the short reveal link).</param>
public sealed record RevealToken(Guid EventId, Guid GiverMemberId, string Token);

/// <summary>What a reveal token points at.</summary>
/// <param name="GroupId">The group.</param>
/// <param name="EventId">The event.</param>
/// <param name="GiverMemberId">The giver.</param>
public sealed record RevealTarget(Guid GroupId, Guid EventId, Guid GiverMemberId);

/// <summary>The outcome of a write.</summary>
public enum WriteStatus
{
    /// <summary>Written.</summary>
    Applied,

    /// <summary>The group, member or token doesn't exist (or isn't visible to the caller).</summary>
    NotFound,

    /// <summary>The caller lacks the permission.</summary>
    Forbidden,

    /// <summary>The input breaks a bound or a rule.</summary>
    Invalid,

    /// <summary>The write conflicts with the stored state (e.g. a member already taken, an older schema).</summary>
    Conflict,
}

/// <summary>The outcome of a document push.</summary>
/// <param name="Status">Whether it was applied.</param>
/// <param name="Version">The stored version afterwards.</param>
/// <param name="Document">The stored document afterwards (member entries merged in).</param>
/// <param name="Rewritten">Whether the stored document differs from the pushed one, so the pusher must adopt it.</param>
public sealed record PushResult(WriteStatus Status, long Version, GroupState? Document, bool Rewritten);
