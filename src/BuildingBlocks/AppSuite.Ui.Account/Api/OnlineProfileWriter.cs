using AppSuite.ApiClient;
using AppSuite.Main.Contracts;

namespace AppSuite.Ui.Account.Api;

/// <summary>
/// The direct-online <see cref="IProfileWriter"/> (ADR-0035): a profile read is a <c>GET /profile</c>
/// and a save is a <c>PUT /profile</c>, straight through the account API. This is the default a host
/// gets from <c>AddAccountUi</c>, and the whole behaviour for an app with no offline story (the Main
/// app). A host that works offline replaces it with one that queues the change locally.
/// </summary>
/// <param name="api">The account API client.</param>
public sealed class OnlineProfileWriter(AccountApiClient api) : IProfileWriter
{
    /// <inheritdoc />
    public Task<ApiResult<ProfileResponse>> LoadAsync(CancellationToken cancellationToken = default) =>
        api.GetProfileAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ApiResult<ProfileResponse>> SaveAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default) =>
        api.UpdateProfileAsync(request, cancellationToken);
}
