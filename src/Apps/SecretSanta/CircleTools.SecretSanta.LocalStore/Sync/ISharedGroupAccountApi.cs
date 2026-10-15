using CircleTools.ApiClient;
using CircleTools.Main.Contracts;

namespace CircleTools.SecretSanta.LocalStore;

/// <summary>
/// The <strong>bearer-authorized</strong> shared-group operations (ADR-0058, FEAT-013): claiming admin
/// into an account and releasing it back to shared-by-link. Unlike <see cref="ISharedGroupApi"/> these
/// require a signed-in caller, so the host wires this over the account's authorized client.
/// </summary>
public interface ISharedGroupAccountApi
{
    /// <summary>Claims admin of a shared-by-link group into the signed-in account (retires the admin link).</summary>
    Task<ApiResult> ClaimAdminAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Releases an account-owned group back to shared-by-link, minting a fresh admin link.</summary>
    Task<ApiResult<RotatedAdminLink>> ReleaseAdminAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Reads a group the caller owns, by group id + bearer (My account mode, D2).</summary>
    Task<ApiResult<SharedGroupView>> GetOwnedAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for a group the caller owns, by group id + bearer (My account mode, D2).</summary>
    Task<ApiResult<PushSharedGroupResult>> PushOwnedAsync(Guid groupId, PushSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the online copy of a group the caller owns, by group id + bearer (My account mode, ADR-0078).
    /// Succeeds too when the group is already gone.
    /// </summary>
    Task<ApiResult> DeleteOwnedAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Lists the groups the caller owns, so this device can discover claimed groups (D2 discovery).</summary>
    Task<ApiResult<OwnedSharedGroups>> ListOwnedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the per-giver reveal slices for a group the caller owns (account mode, ADR-0068, FEAT-020):
    /// the owner authorizes by group id + bearer, having no admin token. Same set-replace semantics as the
    /// admin-token publish.
    /// </summary>
    Task<ApiResult> PublishOwnedRevealSlicesAsync(Guid groupId, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default);
}
