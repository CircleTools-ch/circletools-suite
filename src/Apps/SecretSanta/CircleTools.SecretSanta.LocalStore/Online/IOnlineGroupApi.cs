using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Online;

/// <summary>
/// What a request proves besides the account (ADR-0086): the organizer link's token and/or a device key. The
/// account travels as the bearer whenever someone is signed in.
/// </summary>
/// <param name="OrganizerToken">The organizer link's token, if this device holds one.</param>
/// <param name="DeviceKey">The device key, if this device joined without an account.</param>
public sealed record OnlineCredentials(string? OrganizerToken = null, string? DeviceKey = null)
{
    /// <summary>Gets credentials that prove nothing beyond the bearer.</summary>
    public static OnlineCredentials None { get; } = new();
}

/// <summary>
/// The client's port onto the Secret-Santa group store (ADR-0086, FEAT-027): one API for every online group, every
/// role. An unreachable server is <see cref="ApiFailure.Unreachable"/>, never an exception.
/// </summary>
public interface IOnlineGroupApi
{
    /// <summary>Makes a group online.</summary>
    Task<ApiResult<GroupLinks>> CreateAsync(CreateOnlineGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads the whole group (read-document permission).</summary>
    Task<ApiResult<OrganizerGroupView>> GetAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new document version (write-document permission).</summary>
    Task<ApiResult<PushOnlineGroupResponse>> PushAsync(Guid groupId, PushOnlineGroupRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Deletes the online copy (delete-group permission).</summary>
    Task<ApiResult> DeleteAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Turns the join link on or off, optionally replacing it.</summary>
    Task<ApiResult<GroupLinks>> SetJoinLinkAsync(Guid groupId, JoinLinkRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Replaces the organizer link of a link-owned group.</summary>
    Task<ApiResult<GroupLinks>> RotateOrganizerLinkAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>The signed-in account takes over a link-owned group.</summary>
    Task<ApiResult> TakeOverAsync(Guid groupId, string organizerToken, CancellationToken cancellationToken = default);

    /// <summary>The owning account hands the group back to an organizer link.</summary>
    Task<ApiResult<GroupLinks>> HandOverAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Brings a person in as a member (own member, invitation, circle).</summary>
    Task<ApiResult> AddParticipantAsync(Guid groupId, AddParticipantRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Releases a participant or withdraws an invitation.</summary>
    Task<ApiResult> ReleaseAsync(Guid groupId, Guid memberId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>The reveal tokens of the current draws' givers.</summary>
    Task<ApiResult<IReadOnlyList<RevealTokenView>>> GetRevealTokensAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>A participant's own view.</summary>
    Task<ApiResult<ParticipantView>> GetParticipantViewAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>A participant's own entry.</summary>
    Task<ApiResult<OwnEntryResponse>> WriteOwnEntryAsync(Guid groupId, OwnEntryRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>A participant leaves.</summary>
    Task<ApiResult> LeaveAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>What the join link shows.</summary>
    Task<ApiResult<JoinPreview>> PreviewJoinAsync(string joinToken, CancellationToken cancellationToken = default);

    /// <summary>Takes part through the join link.</summary>
    Task<ApiResult<JoinResponse>> JoinAsync(string joinToken, JoinRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every group the signed-in account has a role in.</summary>
    Task<ApiResult<MyGroups>> GetMyGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Answers an email invitation.</summary>
    Task<ApiResult> AnswerInvitationAsync(Guid groupId, bool accept, CancellationToken cancellationToken = default);

    /// <summary>The reveal page behind a token.</summary>
    Task<ApiResult<RevealPayload>> GetRevealAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>The entry a reveal-link holder may edit.</summary>
    Task<ApiResult<RevealEntry>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default);

    /// <summary>Writes a reveal-link holder's entry.</summary>
    Task<ApiResult> WriteRevealEntryAsync(string revealToken, RevealEntry entry, CancellationToken cancellationToken = default);
}
