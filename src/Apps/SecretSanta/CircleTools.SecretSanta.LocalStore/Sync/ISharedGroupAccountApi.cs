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

    /// <summary>Claims a member through the join link for the signed-in account (ADR-0080): the bearer binds it.</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="request">Which member.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The claim outcome, with the participant key.</returns>
    Task<ApiResult<PersonClaimResponse>> ClaimPersonAsync(string joinToken, ClaimPersonRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads a group by its join token as the signed-in account's claimed member (ADR-0080).</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The trimmed group view.</returns>
    Task<ApiResult<SharedGroupView>> GetJoinedAsync(string joinToken, CancellationToken cancellationToken = default);

    /// <summary>Writes the signed-in account's own entry in a joined group (ADR-0080).</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="entry">The whole entry.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The group version now stored.</returns>
    Task<ApiResult<ParticipantEntryResult>> WriteEntryAsync(string joinToken, ParticipantEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Leaves a joined group as the signed-in account (ADR-0080, Q1).</summary>
    /// <param name="joinToken">The join token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success when the claim was released.</returns>
    Task<ApiResult> LeaveAsync(string joinToken, CancellationToken cancellationToken = default);

    /// <summary>Lists the shared groups the signed-in account joined, so this device adds them (ADR-0080).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The joined groups.</returns>
    Task<ApiResult<JoinedSharedGroups>> ListJoinedAsync(CancellationToken cancellationToken = default);

    /// <summary>Frees a joined person of a group the account owns (ADR-0081).</summary>
    /// <param name="groupId">The owned group.</param>
    /// <param name="memberId">The member to free.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success when freed.</returns>
    Task<ApiResult> ReleasePersonAsOwnerAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes the per-giver reveal slices for a group the caller owns (account mode, ADR-0068, FEAT-020):
    /// the owner authorizes by group id + bearer, having no admin token. Same set-replace semantics as the
    /// admin-token publish.
    /// </summary>
    /// <returns>The tokens in effect for keyed slices — an existing token wins over the one sent (ADR-0068 amendment).</returns>
    Task<ApiResult<PublishRevealSlicesResponse>> PublishOwnedRevealSlicesAsync(Guid groupId, PublishRevealSlicesRequest request, CancellationToken cancellationToken = default);
}
