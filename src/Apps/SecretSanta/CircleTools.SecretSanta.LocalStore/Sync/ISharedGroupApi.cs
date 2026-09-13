using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The client's port onto the account-free shared-by-link endpoints (ADR-0058, FEAT-013,
/// <c>/api/v1/shared</c>). Unlike <see cref="ISyncApi"/> these calls are token-addressed and carry no
/// bearer — the share token is the credential. An unreachable server is an <see cref="ApiResult"/>
/// value the engine inspects, never an exception that leaves local state half-flipped.
/// </summary>
public interface ISharedGroupApi
{
    /// <summary>Uploads a group into shared-by-link storage and mints its admin + join links.</summary>
    Task<ApiResult<MintedLinks>> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Fetches the group a token addresses (or a not-found the caller reads as "gone").</summary>
    Task<ApiResult<SharedGroupView>> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for the group an admin token addresses.</summary>
    Task<ApiResult<PushSharedGroupResult>> PushAsync(string token, PushSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Disables the current admin link and mints a fresh one.</summary>
    Task<ApiResult<RotatedAdminLink>> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Deletes the online copy of a group and all its links (owner-less delete, NFR-010).</summary>
    Task<ApiResult> DeleteAsync(string adminToken, CancellationToken cancellationToken = default);
}
