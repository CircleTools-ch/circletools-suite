using CircleTools.ApiClient;
using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// A scripted <see cref="IOnlineGroupApi"/> (ADR-0086): it records what was asked and answers with what a test set;
/// anything not set answers "unreachable", so a page under test behaves as offline unless told otherwise.
/// </summary>
public sealed class FakeOnlineGroupApi : IOnlineGroupApi
{
    private static readonly ApiError Offline = new(ApiFailure.Unreachable);

    /// <summary>Gets the participants added, in order.</summary>
    public List<(Guid GroupId, AddParticipantRequest Request)> Added { get; } = [];

    /// <summary>Gets the members released, in order.</summary>
    public List<(Guid GroupId, Guid MemberId)> Released { get; } = [];

    /// <summary>Gets or sets the failure the next add answers with, or <see langword="null"/> for success.</summary>
    public ApiFailure? AddFailure { get; set; }

    /// <summary>Gets or sets the reveal tokens answered, or <see langword="null"/> for offline.</summary>
    public IReadOnlyList<RevealTokenView>? RevealTokens { get; set; }

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> CreateAsync(CreateOnlineGroupRequest request, CancellationToken cancellationToken = default) => Fail<GroupLinks>();

    /// <inheritdoc />
    public Task<ApiResult<OrganizerGroupView>> GetAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        View is { } view ? Task.FromResult(ApiResult.Ok(view)) : Fail<OrganizerGroupView>();

    /// <summary>Gets or sets what reading the group answers, or <see langword="null"/> for offline.</summary>
    public OrganizerGroupView? View { get; set; }

    /// <inheritdoc />
    public Task<ApiResult<PushOnlineGroupResponse>> PushAsync(Guid groupId, PushOnlineGroupRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail<PushOnlineGroupResponse>();

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail();

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> SetJoinLinkAsync(Guid groupId, JoinLinkRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail<GroupLinks>();

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> RotateOrganizerLinkAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail<GroupLinks>();

    /// <inheritdoc />
    public Task<ApiResult> TakeOverAsync(Guid groupId, string organizerToken, CancellationToken cancellationToken = default) => Fail();

    /// <inheritdoc />
    public Task<ApiResult<GroupLinks>> HandOverAsync(Guid groupId, CancellationToken cancellationToken = default) => Fail<GroupLinks>();

    /// <inheritdoc />
    public Task<ApiResult> AddParticipantAsync(Guid groupId, AddParticipantRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default)
    {
        Added.Add((groupId, request));
        return Task.FromResult(AddFailure is { } failure ? ApiResult.Failed(new ApiError(failure)) : ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult> ReleaseAsync(Guid groupId, Guid memberId, OnlineCredentials credentials, CancellationToken cancellationToken = default)
    {
        Released.Add((groupId, memberId));
        return Task.FromResult(ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<RevealTokenView>>> GetRevealTokensAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult(RevealTokens is { } tokens ? ApiResult.Ok(tokens) : ApiResult.Failed<IReadOnlyList<RevealTokenView>>(Offline));

    /// <inheritdoc />
    public Task<ApiResult<ParticipantView>> GetParticipantViewAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail<ParticipantView>();

    /// <inheritdoc />
    public Task<ApiResult<OwnEntryResponse>> WriteOwnEntryAsync(Guid groupId, OwnEntryRequest request, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail<OwnEntryResponse>();

    /// <inheritdoc />
    public Task<ApiResult> LeaveAsync(Guid groupId, OnlineCredentials credentials, CancellationToken cancellationToken = default) => Fail();

    /// <summary>Gets or sets what the join link shows, or <see langword="null"/> for offline.</summary>
    public JoinPreview? Preview { get; set; }

    /// <inheritdoc />
    public Task<ApiResult<JoinPreview>> PreviewJoinAsync(string joinToken, CancellationToken cancellationToken = default) =>
        Preview is { } preview ? Task.FromResult(ApiResult.Ok(preview)) : Fail<JoinPreview>();

    /// <inheritdoc />
    public Task<ApiResult<JoinResponse>> JoinAsync(string joinToken, JoinRequest request, CancellationToken cancellationToken = default) => Fail<JoinResponse>();

    /// <inheritdoc />
    public Task<ApiResult<MyGroups>> GetMyGroupsAsync(CancellationToken cancellationToken = default) => Fail<MyGroups>();

    /// <inheritdoc />
    public Task<ApiResult> AnswerInvitationAsync(Guid groupId, bool accept, CancellationToken cancellationToken = default) => Fail();

    // ---------------------------------------------------------------- reveal links (ADR-0068/0084, derived per ADR-0086)

    private readonly Dictionary<string, RevealPayload> _pages = [];
    private readonly Dictionary<string, RevealEntry> _entries = [];

    /// <summary>Gets or sets whether reveal reads report the server unreachable (the offline fallback).</summary>
    public bool RevealSlicesUnreachable { get; set; }

    /// <summary>Gets or sets whether own-entry writes report the server unreachable.</summary>
    public bool RevealEntryWritesFail { get; set; }

    /// <summary>Gets the own-entry writes received, in order.</summary>
    public List<(string RevealToken, RevealEntry Entry)> RevealEntryWrites { get; } = [];

    /// <summary>Seeds the page a reveal token opens, from its payload JSON.</summary>
    /// <param name="revealToken">The token.</param>
    /// <param name="bodyJson">The payload JSON (the reveal format).</param>
    public void SeedRevealSlice(string revealToken, string bodyJson) =>
        _pages[revealToken] = System.Text.Json.JsonSerializer.Deserialize(bodyJson, CircleTools.SecretSanta.Core.Reveal.RevealJsonContext.Default.RevealPayload)!;

    /// <summary>Seeds the holder's own entry, so the reveal page offers the editor.</summary>
    /// <param name="revealToken">The token.</param>
    /// <param name="entry">The entry.</param>
    public void SeedRevealEntry(string revealToken, RevealEntry entry) => _entries[revealToken] = entry;

    /// <inheritdoc />
    public Task<ApiResult<RevealPayload>> GetRevealAsync(string revealToken, CancellationToken cancellationToken = default) =>
        RevealSlicesUnreachable ? Fail<RevealPayload>()
        : _pages.TryGetValue(revealToken, out var page) ? Task.FromResult(ApiResult.Ok(page))
        : Task.FromResult(ApiResult.Failed<RevealPayload>(new ApiError(ApiFailure.NotFound)));

    /// <inheritdoc />
    public Task<ApiResult<RevealEntry>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default) =>
        RevealSlicesUnreachable ? Fail<RevealEntry>()
        : _entries.TryGetValue(revealToken, out var entry) ? Task.FromResult(ApiResult.Ok(entry))
        : Task.FromResult(ApiResult.Failed<RevealEntry>(new ApiError(ApiFailure.NotFound)));

    /// <inheritdoc />
    public Task<ApiResult> WriteRevealEntryAsync(string revealToken, RevealEntry entry, CancellationToken cancellationToken = default)
    {
        if (RevealEntryWritesFail)
        {
            return Fail();
        }

        RevealEntryWrites.Add((revealToken, entry));
        _entries[revealToken] = entry;
        return Task.FromResult(ApiResult.Ok());
    }

    private static Task<ApiResult> Fail() => Task.FromResult(ApiResult.Failed(Offline));

    private static Task<ApiResult<T>> Fail<T>() => Task.FromResult(ApiResult.Failed<T>(Offline));
}

/// <summary>Seeds this device's group records (ADR-0086) for a page under test.</summary>
public static class OnlineRecords
{
    /// <summary>A record store over an in-memory key-value store, holding the given records.</summary>
    /// <param name="keyValues">The key-value store the page also uses.</param>
    /// <param name="records">The records.</param>
    /// <returns>The store.</returns>
    public static GroupRecordStore Seed(IKeyValueStore keyValues, params GroupRecord[] records)
    {
        var store = new GroupRecordStore(keyValues);
        store.SaveAsync(GroupRecordsDocument.Empty with { Records = records }).GetAwaiter().GetResult();
        return store;
    }

    /// <summary>An online group this device organizes, with the owner's permissions.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="roles">Who has a role in it.</param>
    /// <param name="selfMaintained">Members who keep their own entry.</param>
    /// <returns>The record.</returns>
    public static GroupRecord Organized(Guid groupId, IReadOnlyList<GroupRoleView>? roles = null, IReadOnlyList<Guid>? selfMaintained = null) =>
        new(groupId, GroupPlace.Online, new GroupState(groupId, "Familie", null, null, [], [], []), null, 1, false,
            new OnlineAccess(AccessPrincipal.Account, "owner",
                [OnlinePermissions.ReadDocument, OnlinePermissions.WriteDocument, OnlinePermissions.ManageParticipants,
                 OnlinePermissions.ManageLinks, OnlinePermissions.ManageRoles, OnlinePermissions.DeleteGroup],
                AccountId: Guid.NewGuid()),
            SelfMaintained: selfMaintained ?? [],
            Roles: roles ?? [new GroupRoleView("owner", "account", null, false, "created")]);

    /// <summary>A group kept on this device only.</summary>
    /// <param name="groupId">The group.</param>
    /// <returns>The record.</returns>
    public static GroupRecord OnDevice(Guid groupId) =>
        new(groupId, GroupPlace.Device, new GroupState(groupId, "Familie", null, null, [], [], []), null, 0, false);
}
