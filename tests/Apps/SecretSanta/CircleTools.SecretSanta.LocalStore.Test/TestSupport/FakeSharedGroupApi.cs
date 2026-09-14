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
    private int _counter;

    /// <summary>Set to make the next call behave as if the server is unreachable.</summary>
    public bool Unreachable { get; set; }

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

    /// <inheritdoc />
    public Task<ApiResult<MintedLinks>> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default)
    {
        if (Unreachable)
        {
            return Fail<MintedLinks>(ApiFailure.Unreachable);
        }

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

        return Task.FromResult(ApiResult.Ok(new PersonClaimResponse(true, false)));
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
        if (Unreachable)
        {
            return Fail<SharedGroupView>(ApiFailure.Unreachable);
        }

        // The fake treats a claimed (admin-retired) group as owned; it has no bearer context.
        if (!_groups.TryGetValue(groupId, out var g) || !g.AdminRetired)
        {
            return Fail<SharedGroupView>(ApiFailure.NotFound);
        }

        return Task.FromResult(ApiResult.Ok(new SharedGroupView(g.Id, g.Version, Parse(g.Body), SharedCapability.Admin, false, null)));
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
