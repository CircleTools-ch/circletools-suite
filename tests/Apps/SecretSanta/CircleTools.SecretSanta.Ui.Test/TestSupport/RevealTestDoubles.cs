using CircleTools.ApiClient;
using CircleTools.Main.Contracts;
using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// An empty <see cref="ILocalSyncStore"/> for component tests that only need the store to resolve: it
/// holds no groups, so a page reads no reveal tokens (ADR-0068) and behaves as a local-only view.
/// </summary>
public sealed class EmptyLocalSyncStore : ILocalSyncStore
{
    private readonly string _deviceId = Guid.NewGuid().ToString("N");

    public Task<IReadOnlyList<SyncedGroupRecord>> LoadGroupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SyncedGroupRecord>>([]);

    public Task SaveGroupsAsync(IReadOnlyList<SyncedGroupRecord> records, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<SyncState> LoadStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SyncState(_deviceId, [], []));

    public Task SaveStateAsync(SyncState state, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SyncedProfileRecord?> LoadProfileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SyncedProfileRecord?>(null);

    public Task SaveProfileAsync(SyncedProfileRecord profile, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<SyncedPreferencesRecord?> LoadPreferencesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<SyncedPreferencesRecord?>(null);

    public Task SavePreferencesAsync(SyncedPreferencesRecord preferences, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// A shared-group API for component tests: unreachable by default, but a reveal slice can be seeded so
/// the reveal page's live-fetch path (ADR-0068) is exercised. Set <see cref="RevealSlicesUnreachable"/>
/// to force the offline fallback even with a seeded slice.
/// </summary>
public sealed class NoOpSharedGroupApi : ISharedGroupApi, ISharedGroupAccountApi
{
    private readonly Dictionary<string, System.Text.Json.JsonElement> _slices = [];

    /// <summary>When true, <see cref="GetRevealSliceAsync"/> reports the server unreachable (offline fallback).</summary>
    public bool RevealSlicesUnreachable { get; set; }

    /// <summary>Seeds a live reveal slice for <paramref name="revealToken"/> (its raw payload JSON).</summary>
    public void SeedRevealSlice(string revealToken, string bodyJson)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(bodyJson);
        _slices[revealToken] = doc.RootElement.Clone();
    }

    public Task<ApiResult<MintedLinks>> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default) => Unreachable<MintedLinks>();
    public Task<ApiResult<SharedGroupView>> GetAsync(string token, CancellationToken cancellationToken = default) => Unreachable<SharedGroupView>();
    public Task<ApiResult<PushSharedGroupResult>> PushAsync(string token, PushSharedGroupRequest request, CancellationToken cancellationToken = default) => Unreachable<PushSharedGroupResult>();
    public Task<ApiResult<RotatedAdminLink>> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default) => Unreachable<RotatedAdminLink>();
    public Task<ApiResult> DeleteAsync(string adminToken, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<ClaimedMembers>> GetClaimsAsync(string token, CancellationToken cancellationToken = default) => Unreachable<ClaimedMembers>();
    public Task<ApiResult<PersonClaimResponse>> ClaimPersonAsync(string token, ClaimPersonRequest request, CancellationToken cancellationToken = default) => Unreachable<PersonClaimResponse>();
    public Task<ApiResult> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<PublishRevealSlicesResponse>> PublishRevealSlicesAsync(string adminToken, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default) => Unreachable<PublishRevealSlicesResponse>();

    public Task<ApiResult<RevealSliceView>> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default) =>
        RevealSlicesUnreachable ? Unreachable<RevealSliceView>()
        : _slices.TryGetValue(revealToken, out var body) ? Task.FromResult(ApiResult.Ok(new RevealSliceView(body)))
        : Task.FromResult(ApiResult.Failed<RevealSliceView>(new ApiError(ApiFailure.NotFound)));

    private readonly Dictionary<string, RevealEntryView> _entries = [];

    /// <summary>The own-entry writes received (ADR-0084), in order.</summary>
    public List<(string RevealToken, RevealEntryWrite Entry)> RevealEntryWrites { get; } = [];

    /// <summary>When true, own-entry writes report the server unreachable.</summary>
    public bool RevealEntryWritesFail { get; set; }

    /// <summary>Seeds the holder's own entry for <paramref name="revealToken"/>, so the reveal page offers the editor.</summary>
    public void SeedRevealEntry(string revealToken, RevealEntryView entry) => _entries[revealToken] = entry;

    public Task<ApiResult<RevealEntryView>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default) =>
        RevealSlicesUnreachable ? Unreachable<RevealEntryView>()
        : _entries.TryGetValue(revealToken, out var entry) ? Task.FromResult(ApiResult.Ok(entry))
        : Task.FromResult(ApiResult.Failed<RevealEntryView>(new ApiError(ApiFailure.NotFound)));

    public Task<ApiResult<ParticipantEntryResult>> WriteRevealEntryAsync(string revealToken, RevealEntryWrite entry, CancellationToken cancellationToken = default)
    {
        if (RevealEntryWritesFail)
        {
            return Unreachable<ParticipantEntryResult>();
        }

        RevealEntryWrites.Add((revealToken, entry));
        _entries[revealToken] = new RevealEntryView(entry.GiftNote, entry.Items);
        return Task.FromResult(ApiResult.Ok(new ParticipantEntryResult(RevealEntryWrites.Count + 1)));
    }

    public Task<ApiResult> ClaimAdminAsync(string adminToken, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<RotatedAdminLink>> ReleaseAdminAsync(Guid groupId, CancellationToken cancellationToken = default) => Unreachable<RotatedAdminLink>();
    public Task<ApiResult<SharedGroupView>> GetOwnedAsync(Guid groupId, CancellationToken cancellationToken = default) => Unreachable<SharedGroupView>();
    public Task<ApiResult<PushSharedGroupResult>> PushOwnedAsync(Guid groupId, PushSharedGroupRequest request, CancellationToken cancellationToken = default) => Unreachable<PushSharedGroupResult>();
    public Task<ApiResult> DeleteOwnedAsync(Guid groupId, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<OwnedSharedGroups>> ListOwnedAsync(CancellationToken cancellationToken = default) => Unreachable<OwnedSharedGroups>();
    public Task<ApiResult<PublishRevealSlicesResponse>> PublishOwnedRevealSlicesAsync(Guid groupId, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default) => Unreachable<PublishRevealSlicesResponse>();
    public Task<ApiResult<SharedGroupView>> GetAsParticipantAsync(string token, string participantKey, CancellationToken cancellationToken = default) => Unreachable<SharedGroupView>();
    public Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string token, string participantKey, ParticipantEntry entry, CancellationToken cancellationToken = default) => Unreachable<ParticipantEntryResult>();
    public Task<ApiResult> LeaveAsync(string token, string participantKey, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<ParticipantKeyResponse>> IssueParticipantKeyAsync(string token, Guid memberId, string deviceHandle, CancellationToken cancellationToken = default) => Unreachable<ParticipantKeyResponse>();
    public Task<ApiResult<SharedGroupView>> GetJoinedAsync(string joinToken, CancellationToken cancellationToken = default) => Unreachable<SharedGroupView>();
    public Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string joinToken, ParticipantEntry entry, CancellationToken cancellationToken = default) => Unreachable<ParticipantEntryResult>();
    public Task<ApiResult> LeaveAsync(string joinToken, CancellationToken cancellationToken = default) => Unreachable();
    public Task<ApiResult<JoinedSharedGroups>> ListJoinedAsync(CancellationToken cancellationToken = default) => Unreachable<JoinedSharedGroups>();
    public Task<ApiResult> ReleasePersonAsOwnerAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default) => Unreachable();

    private static Task<ApiResult<T>> Unreachable<T>() => Task.FromResult(ApiResult.Failed<T>(new ApiError(ApiFailure.Unreachable)));
    private static Task<ApiResult> Unreachable() => Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
}
