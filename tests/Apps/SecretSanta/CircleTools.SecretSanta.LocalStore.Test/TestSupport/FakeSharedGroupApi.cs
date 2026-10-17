using System.Text.Json;
using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore.Test.TestSupport;

/// <summary>
/// An in-memory stand-in for the shared-by-link server, faithful to
/// <c>SharedGroupStore</c>'s contract, so the client <see cref="SharedSyncService"/> can be driven in
/// a unit test with no transport (FEAT-013). Tests can also seed and inspect the "server" directly.
/// </summary>
public sealed class FakeSharedGroupApi : ISharedGroupApi, ISharedGroupAccountApi
{
    private sealed record Group(Guid Id, string Body, long Version, bool AdminRetired, string? AdminName);

    private readonly Dictionary<Guid, Group> _groups = [];
    private readonly Dictionary<string, (Guid GroupId, SharedCapability Capability, bool Active, bool RetiredByClaim)> _tokens = [];
    private readonly HashSet<(Guid GroupId, Guid MemberId)> _claims = [];
    private readonly Dictionary<string, (Guid GroupId, Guid MemberId)> _participantKeys = [];
    private readonly Dictionary<Guid, string> _creatorKeys = [];
    private int _counter;

    /// <summary>Set to make the next call behave as if the server is unreachable.</summary>
    public bool Unreachable { get; set; }

    /// <summary>Gets the group ids read through the owner path (<c>GET /shared/{id}/owned</c>), in call order.</summary>
    public List<Guid> OwnedReads { get; } = [];

    /// <summary>Gets the group ids whose reveal slices were published through the owner path.</summary>
    public List<Guid> OwnedSlicePublishes { get; } = [];

    /// <summary>The creator key the client last presented on a create for <paramref name="groupId"/>, or null.</summary>
    public string? CreatorKeyFor(Guid groupId) => _creatorKeys.GetValueOrDefault(groupId);

    /// <summary>Directly sets a group's server body/version, simulating a push from another device.</summary>
    public void SeedServer(Guid groupId, string body, long version)
    {
        var existing = _groups.GetValueOrDefault(groupId);
        _groups[groupId] = new Group(groupId, body, version, existing?.AdminRetired ?? false, existing?.AdminName);
    }

    /// <summary>Marks a group's admin link retired by an account claim (simulating Phase-G claim-admin).</summary>
    public void RetireAdmin(Guid groupId, string name)
    {
        if (_groups.TryGetValue(groupId, out var g))
        {
            _groups[groupId] = g with { AdminRetired = true, AdminName = name };
            foreach (var key in _tokens.Keys.ToList())
            {
                var t = _tokens[key];
                if (t.GroupId == groupId && t.Capability == SharedCapability.Admin)
                {
                    _tokens[key] = t with { Active = false, RetiredByClaim = true };
                }
            }
        }
    }

    /// <summary>Reads a group's current server version, or null if it is gone.</summary>
    public long? ServerVersion(Guid groupId) => _groups.TryGetValue(groupId, out var g) ? g.Version : null;

    /// <summary>Gets or sets an action run inside every <see cref="GetAsync"/>, as if during the round trip.</summary>
    public Action? DuringGet { get; set; }

    /// <summary>The stored body of a group, or <see langword="null"/>.</summary>
    /// <param name="groupId">The group.</param>
    /// <returns>The raw JSON body.</returns>
    public string? ServerBody(Guid groupId) => _groups.TryGetValue(groupId, out var g) ? g.Body : null;

    /// <inheritdoc />
    public Task<ApiResult<MintedLinks>> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<MintedLinks>(ApiFailure.Unreachable);
        }

        _creatorKeys[request.GroupId] = request.CreatorKey;

        if (!_groups.ContainsKey(request.GroupId))
        {
            _groups[request.GroupId] = new Group(request.GroupId, request.Body.GetRawText(), request.Version, false, null);
        }

        var admin = _tokens.FirstOrDefault(t => t.Value is { Active: true, Capability: SharedCapability.Admin } && t.Value.GroupId == request.GroupId).Key
            ?? Mint(request.GroupId, SharedCapability.Admin);
        var join = _tokens.FirstOrDefault(t => t.Value is { Active: true, Capability: SharedCapability.Join } && t.Value.GroupId == request.GroupId).Key
            ?? Mint(request.GroupId, SharedCapability.Join);

        return Task.FromResult(ApiResult.Ok(new MintedLinks(request.GroupId, admin, join)));
    }

    /// <inheritdoc />
    public Task<ApiResult<SharedGroupView>> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        // Lets a test act while the read is "in flight" (a local edit racing the reconcile).
        DuringGet?.Invoke();
        if (Unreachable)
        {
            return Fail<SharedGroupView>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(token, out var t) || !_groups.TryGetValue(t.GroupId, out var g))
        {
            return Fail<SharedGroupView>(ApiFailure.NotFound);
        }

        if (!t.Active)
        {
            if (t is { RetiredByClaim: true, Capability: SharedCapability.Admin })
            {
                return Task.FromResult(ApiResult.Ok(new SharedGroupView(g.Id, g.Version, Parse(g.Body), SharedCapability.Admin, true, g.AdminName)));
            }

            return Fail<SharedGroupView>(ApiFailure.NotFound);
        }

        return Task.FromResult(ApiResult.Ok(new SharedGroupView(g.Id, g.Version, Parse(g.Body), t.Capability, false, null)));
    }

    /// <inheritdoc />
    public Task<ApiResult<PushSharedGroupResult>> PushAsync(string token, PushSharedGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<PushSharedGroupResult>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(token, out var t) || !g_exists(t))
        {
            return Fail<PushSharedGroupResult>(ApiFailure.NotFound);
        }

        if (!t.Active || t.Capability != SharedCapability.Admin)
        {
            return Fail<PushSharedGroupResult>(ApiFailure.Forbidden);
        }

        var g = _groups[t.GroupId];
        var collided = request.Version <= g.Version;
        var version = Math.Max(request.Version, g.Version);

        // Simulates the server re-applying participants' entries to the pushed body (ADR-0080).
        if (RewritePushes is { } rewrite)
        {
            _groups[t.GroupId] = g with { Body = rewrite(request.Body.GetRawText()), Version = version };
            return Task.FromResult(ApiResult.Ok(new PushSharedGroupResult(version, collided, Rewritten: true)));
        }

        _groups[t.GroupId] = g with { Body = request.Body.GetRawText(), Version = version };
        return Task.FromResult(ApiResult.Ok(new PushSharedGroupResult(version, collided)));

        bool g_exists((Guid GroupId, SharedCapability, bool, bool) tok) => _groups.ContainsKey(tok.GroupId);
    }

    /// <inheritdoc />
    public Task<ApiResult<RotatedAdminLink>> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<RotatedAdminLink>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(adminToken, out var t) || t is not { Active: true, Capability: SharedCapability.Admin })
        {
            return Fail<RotatedAdminLink>(ApiFailure.Forbidden);
        }

        _tokens[adminToken] = t with { Active = false };
        var fresh = Mint(t.GroupId, SharedCapability.Admin);
        return Task.FromResult(ApiResult.Ok(new RotatedAdminLink(fresh)));
    }

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
        }

        if (!_tokens.TryGetValue(adminToken, out var t) || t is not { Active: true, Capability: SharedCapability.Admin })
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Forbidden)));
        }

        _groups.Remove(t.GroupId);
        foreach (var key in _tokens.Where(x => x.Value.GroupId == t.GroupId).Select(x => x.Key).ToList())
        {
            _tokens.Remove(key);
        }

        return Task.FromResult(ApiResult.Ok());
    }

    /// <summary>Directly marks a member claimed on the server, simulating another joiner.</summary>
    public void SeedClaim(Guid groupId, Guid memberId) => _claims.Add((groupId, memberId));

    /// <inheritdoc />
    public Task<ApiResult<ClaimedMembers>> GetClaimsAsync(string token, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<ClaimedMembers>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(token, out var t) || !t.Active || !_groups.ContainsKey(t.GroupId))
        {
            return Fail<ClaimedMembers>(ApiFailure.NotFound);
        }

        var ids = _claims.Where(c => c.GroupId == t.GroupId).Select(c => c.MemberId).ToList();
        return Task.FromResult(ApiResult.Ok(new ClaimedMembers(ids)));
    }

    /// <inheritdoc />
    public Task<ApiResult<PersonClaimResponse>> ClaimPersonAsync(string token, ClaimPersonRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<PersonClaimResponse>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(token, out var t) || !t.Active || !_groups.ContainsKey(t.GroupId))
        {
            return Fail<PersonClaimResponse>(ApiFailure.NotFound);
        }

        var key = (t.GroupId, request.MemberId);
        if (!_claims.Add(key))
        {
            return Task.FromResult(ApiResult.Ok(new PersonClaimResponse(false, true)));
        }

        var participantKey = $"pk-{++_counter}";
        _participantKeys[participantKey] = key;
        return Task.FromResult(ApiResult.Ok(new PersonClaimResponse(true, false, participantKey)));
    }

    /// <inheritdoc />
    public Task<ApiResult> ReleasePersonAsync(string adminToken, Guid memberId, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
        }

        if (!_tokens.TryGetValue(adminToken, out var t) || t is not { Active: true, Capability: SharedCapability.Admin })
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Forbidden)));
        }

        _claims.Remove((t.GroupId, memberId));
        return Task.FromResult(ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult> ClaimAdminAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
        }

        if (!_tokens.TryGetValue(adminToken, out var t) || t is not { Active: true, Capability: SharedCapability.Admin })
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Forbidden)));
        }

        RetireAdmin(t.GroupId, "Account owner");
        return Task.FromResult(ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult<RotatedAdminLink>> ReleaseAdminAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<RotatedAdminLink>(ApiFailure.Unreachable);
        }

        if (!_groups.TryGetValue(groupId, out var g))
        {
            return Fail<RotatedAdminLink>(ApiFailure.NotFound);
        }

        _groups[groupId] = g with { AdminRetired = false, AdminName = null };
        var fresh = Mint(groupId, SharedCapability.Admin);
        return Task.FromResult(ApiResult.Ok(new RotatedAdminLink(fresh)));
    }

    /// <inheritdoc />
    public Task<ApiResult<SharedGroupView>> GetOwnedAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        OwnedReads.Add(groupId);
        if (Unreachable)
        {
            return Fail<SharedGroupView>(ApiFailure.Unreachable);
        }

        // The fake treats a claimed (admin-retired) group as owned; it has no bearer context. Like the server,
        // a gone group is NotFound and one that exists but is not owned is Forbidden (ADR-0078).
        if (!_groups.TryGetValue(groupId, out var g))
        {
            return Fail<SharedGroupView>(ApiFailure.NotFound);
        }

        if (!g.AdminRetired)
        {
            return Fail<SharedGroupView>(ApiFailure.Forbidden);
        }

        // Like the server, the owner read carries the group's current join token (ADR-0078 Amendment 3).
        var join = _tokens.FirstOrDefault(t => t.Value.GroupId == groupId && t.Value is { Capability: SharedCapability.Join, Active: true }).Key;
        return Task.FromResult(ApiResult.Ok(new SharedGroupView(g.Id, g.Version, Parse(g.Body), SharedCapability.Admin, false, null, join)));
    }

    /// <inheritdoc />
    public Task<ApiResult<PushSharedGroupResult>> PushOwnedAsync(Guid groupId, PushSharedGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<PushSharedGroupResult>(ApiFailure.Unreachable);
        }

        if (!_groups.TryGetValue(groupId, out var g) || !g.AdminRetired)
        {
            return Fail<PushSharedGroupResult>(ApiFailure.NotFound);
        }

        var collided = request.Version <= g.Version;
        var version = Math.Max(request.Version, g.Version);
        _groups[groupId] = g with { Body = request.Body.GetRawText(), Version = version };
        return Task.FromResult(ApiResult.Ok(new PushSharedGroupResult(version, collided)));
    }

    /// <inheritdoc />
    public Task<ApiResult> DeleteOwnedAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
        }

        // Idempotent like the server: an already-gone group is a success. Owned = claimed (admin-retired).
        if (!_groups.TryGetValue(groupId, out var g))
        {
            return Task.FromResult(ApiResult.Ok());
        }

        if (!g.AdminRetired)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Forbidden)));
        }

        _groups.Remove(groupId);
        foreach (var key in _tokens.Where(t => t.Value.GroupId == groupId).Select(t => t.Key).ToList())
        {
            _tokens.Remove(key);
        }

        return Task.FromResult(ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult<OwnedSharedGroups>> ListOwnedAsync(CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<OwnedSharedGroups>(ApiFailure.Unreachable);
        }

        // The fake has no bearer context; a claimed (admin-retired) group counts as owned.
        var owned = _groups.Values
            .Where(g => g.AdminRetired)
            .Select(g => new OwnedSharedGroup(g.Id, g.Version, Parse(g.Body)))
            .ToList();
        return Task.FromResult(ApiResult.Ok(new OwnedSharedGroups(owned)));
    }

    private readonly Dictionary<string, (Guid GroupId, string Body)> _revealSlices = [];

    /// <summary>The reveal tokens the client has published a slice for, mapped to their group (for test assertions).</summary>
    public IReadOnlyDictionary<string, Guid> RevealSliceTokens => _revealSlices.ToDictionary(k => k.Key, v => v.Value.GroupId);

    /// <inheritdoc />
    public Task<ApiResult<PublishRevealSlicesResponse>> PublishRevealSlicesAsync(string adminToken, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed<PublishRevealSlicesResponse>(new ApiError(ApiFailure.Unreachable)));
        }

        if (!_tokens.TryGetValue(adminToken, out var t) || t is not { Active: true, Capability: SharedCapability.Admin })
        {
            return Task.FromResult(ApiResult.Failed<PublishRevealSlicesResponse>(new ApiError(ApiFailure.Forbidden)));
        }

        return Task.FromResult(ApiResult.Ok(ReplaceSlices(t.GroupId, request)));
    }

    /// <inheritdoc />
    public Task<ApiResult<PublishRevealSlicesResponse>> PublishOwnedRevealSlicesAsync(Guid groupId, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default)
    {
        OwnedSlicePublishes.Add(groupId);
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed<PublishRevealSlicesResponse>(new ApiError(ApiFailure.Unreachable)));
        }

        // The fake treats a claimed (admin-retired) group as owned; it has no bearer context.
        if (!_groups.TryGetValue(groupId, out var g) || !g.AdminRetired)
        {
            return Task.FromResult(ApiResult.Failed<PublishRevealSlicesResponse>(new ApiError(ApiFailure.Forbidden)));
        }

        return Task.FromResult(ApiResult.Ok(ReplaceSlices(groupId, request)));
    }

    /// <inheritdoc />
    public Task<ApiResult<RevealSliceView>> GetRevealSliceAsync(string revealToken, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<RevealSliceView>(ApiFailure.Unreachable);
        }

        return _revealSlices.TryGetValue(revealToken, out var slice)
            ? Task.FromResult(ApiResult.Ok(new RevealSliceView(Parse(slice.Body))))
            : Fail<RevealSliceView>(ApiFailure.NotFound);
    }

    /// <inheritdoc />
    public Task<ApiResult<RevealEntryView>> GetRevealEntryAsync(string revealToken, CancellationToken cancellationToken = default) =>
        Fail<RevealEntryView>(ApiFailure.NotFound);

    /// <inheritdoc />
    public Task<ApiResult<ParticipantEntryResult>> WriteRevealEntryAsync(string revealToken, RevealEntryWrite entry, CancellationToken cancellationToken = default) =>
        Fail<ParticipantEntryResult>(ApiFailure.NotFound);

    // Like the server: a keyed slice keeps the token already stored for its key (ADR-0068 amendment).
    private PublishRevealSlicesResponse ReplaceSlices(Guid groupId, PublishRevealSlicesRequest request)
    {
        var kept = _sliceKeys.Where(x => x.Value.GroupId == groupId).ToDictionary(x => x.Value.Key, x => x.Key);
        foreach (var key in _revealSlices.Where(x => x.Value.GroupId == groupId).Select(x => x.Key).ToList())
        {
            _revealSlices.Remove(key);
            _sliceKeys.Remove(key);
        }

        var effective = new List<RevealTokenForKey>();
        foreach (var slice in request.Slices)
        {
            var token = slice.Key is { } k && kept.TryGetValue(k, out var existing) ? existing : slice.RevealToken;
            _revealSlices[token] = (groupId, slice.Body.GetRawText());
            if (slice.Key is { } key)
            {
                _sliceKeys[token] = (groupId, key);
                effective.Add(new RevealTokenForKey(key, token));
            }
        }

        return new PublishRevealSlicesResponse(effective);
    }

    private readonly Dictionary<string, (Guid GroupId, string Key)> _sliceKeys = [];

    /// <summary>When set, every admin push is stored through this rewrite and reported as rewritten (ADR-0080).</summary>
    public Func<string, string>? RewritePushes { get; set; }

    /// <summary>Gets the participant entries written (join token, entry), in call order.</summary>
    public List<(string Token, ParticipantEntry Entry)> WrittenEntries { get; } = [];

    /// <summary>Gets the join tokens a participant left through.</summary>
    public List<string> Left { get; } = [];

    /// <summary>Gets or sets the groups the signed-in account joined, for <see cref="ListJoinedAsync"/>.</summary>
    public List<JoinedSharedGroup> Joined { get; set; } = [];

    /// <summary>Whether a member of the group is claimed (joined).</summary>
    public bool IsClaimed(Guid groupId, Guid memberId) => _claims.Contains((groupId, memberId));

    /// <inheritdoc />
    public async Task<ApiResult<SharedGroupView>> GetAsParticipantAsync(string token, string participantKey, CancellationToken cancellationToken = default)
    {
        var view = await GetAsync(token, cancellationToken);
        return view.IsSuccess && _participantKeys.TryGetValue(participantKey, out var claim)
            ? ApiResult.Ok(view.Value! with { ClaimedMemberId = claim.MemberId, Redacted = true })
            : view;
    }

    /// <inheritdoc />
    public Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string token, string participantKey, ParticipantEntry entry, CancellationToken cancellationToken = default) =>
        _participantKeys.ContainsKey(participantKey)
            ? WriteEntry(token, entry)
            : Fail<ParticipantEntryResult>(Unreachable ? ApiFailure.Unreachable : ApiFailure.Forbidden);

    /// <inheritdoc />
    public Task<ApiResult> LeaveAsync(string token, string participantKey, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Task.FromResult(ApiResult.Failed(new ApiError(ApiFailure.Unreachable)));
        }

        if (_participantKeys.Remove(participantKey, out var claim))
        {
            _claims.Remove(claim);
        }

        Left.Add(token);
        return Task.FromResult(ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult<ParticipantKeyResponse>> IssueParticipantKeyAsync(string token, Guid memberId, string deviceHandle, CancellationToken cancellationToken = default) =>
        Fail<ParticipantKeyResponse>(ApiFailure.Forbidden);

    /// <inheritdoc />
    public Task<ApiResult<SharedGroupView>> GetJoinedAsync(string joinToken, CancellationToken cancellationToken = default) =>
        GetAsync(joinToken, cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string joinToken, ParticipantEntry entry, CancellationToken cancellationToken = default) =>
        WriteEntry(joinToken, entry);

    /// <inheritdoc />
    public Task<ApiResult> LeaveAsync(string joinToken, CancellationToken cancellationToken = default)
    {
        Left.Add(joinToken);
        return Task.FromResult(Unreachable ? ApiResult.Failed(new ApiError(ApiFailure.Unreachable)) : ApiResult.Ok());
    }

    /// <inheritdoc />
    public Task<ApiResult<JoinedSharedGroups>> ListJoinedAsync(CancellationToken cancellationToken = default) =>
        Unreachable ? Fail<JoinedSharedGroups>(ApiFailure.Unreachable) : Task.FromResult(ApiResult.Ok(new JoinedSharedGroups(Joined)));

    /// <inheritdoc />
    public Task<ApiResult> ReleasePersonAsOwnerAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default)
    {
        _claims.Remove((groupId, memberId));
        return Task.FromResult(ApiResult.Ok());
    }

    private Task<ApiResult<ParticipantEntryResult>> WriteEntry(string token, ParticipantEntry entry)
    {
        if (Unreachable)
        {
            return Fail<ParticipantEntryResult>(ApiFailure.Unreachable);
        }

        if (!_tokens.TryGetValue(token, out var t) || !_groups.TryGetValue(t.GroupId, out var g))
        {
            return Fail<ParticipantEntryResult>(ApiFailure.NotFound);
        }

        WrittenEntries.Add((token, entry));
        _groups[t.GroupId] = g with { Version = g.Version + 1 };
        return Task.FromResult(ApiResult.Ok(new ParticipantEntryResult(g.Version + 1)));
    }

    private string Mint(Guid groupId, SharedCapability capability)
    {
        var token = $"{capability}-{groupId:N}-{++_counter}";
        _tokens[token] = (groupId, capability, true, false);
        return token;
    }

    private static Task<ApiResult<T>> Fail<T>(ApiFailure failure) =>
        Task.FromResult(ApiResult.Failed<T>(new ApiError(failure)));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
