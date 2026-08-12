using AppSuite.ApiClient;
using AppSuite.Main.Contracts;

namespace AppSuite.Ui.Account.Api;

/// <summary>
/// How the shared profile screen reads and writes the account's preferences (display name and
/// language), abstracted so the host decides where they go (ADR-0035).
/// </summary>
/// <remarks>
/// The Main app, which has no offline story, registers the direct-online implementation
/// (<see cref="OnlineProfileWriter"/>) — a profile edit is an authorized <c>PUT /profile</c>, exactly
/// as it always was. The Secret-Santa app, which works offline behind a sync engine, registers an
/// implementation that writes a versioned local profile document and lets the sync cycle push it, so a
/// language change made offline is queued and pushed on the next sync (FR-012). Both speak the same
/// <see cref="ProfileResponse"/>/<see cref="UpdateProfileRequest"/> the page already uses, so the page
/// does not know or care which host it is running in.
/// </remarks>
public interface IProfileWriter
{
    /// <summary>Reads the current profile — from wherever this host keeps it (FR-012).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The profile, or the failure to show.</returns>
    Task<ApiResult<ProfileResponse>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves display name and preferred language (FR-012).</summary>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved profile, or the failure to show.</returns>
    Task<ApiResult<ProfileResponse>> SaveAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
