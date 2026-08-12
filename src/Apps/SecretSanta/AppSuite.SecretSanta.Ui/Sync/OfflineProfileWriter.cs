using System.Globalization;
using AppSuite.ApiClient;
using AppSuite.Localization;
using AppSuite.Main.Contracts;
using AppSuite.SecretSanta.LocalStore;
using AppSuite.Ui.Abstractions;
using AppSuite.Ui.Account.Api;
using AppSuite.Ui.Account.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace AppSuite.SecretSanta.Ui;

/// <summary>
/// The offline-capable <see cref="IProfileWriter"/> for the Secret-Santa app (ADR-0035). The
/// authoritative profile stays on the Main service's <c>/account/profile</c> endpoint — the source of
/// the JWT locale and what the Main app reads — so this does not invent a second source of truth. What
/// it adds is local-first behaviour: an edit made offline is written to a local pending record and
/// <c>PUT</c>-flushed on the next sync trigger (FR-012), and a read served from that pending record
/// while offline rather than failing (NFR-001).
/// </summary>
/// <remarks>
/// The pending record is a Platform concern kept in the Secret-Santa local store because that is the
/// only store this app has; it never rides the opaque sync-store cycle a group does (ADR-0035). Cross-
/// device propagation is by the authoritative endpoint: another device adopts the change when it reads
/// the profile — on its next <see cref="ReconcileAsync"/> or when a fresh sign-in carries the locale.
/// </remarks>
/// <param name="api">The account API — the authoritative profile read/write.</param>
/// <param name="local">The local store holding a pending offline edit.</param>
/// <param name="culture">The UI language, switched when another device's change is pulled in.</param>
/// <param name="auth">Resolves the signed-in account the pending edit is scoped to.</param>
public sealed class OfflineProfileWriter(
    AccountApiClient api,
    ILocalSyncStore local,
    ICultureProvider culture,
    AuthenticationStateProvider auth) : IProfileWriter
{
    /// <inheritdoc />
    public async Task<ApiResult<ProfileResponse>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var accountId = await CurrentAccountAsync();
        var pending = await PendingForAsync(accountId, cancellationToken);

        // A queued offline edit is the newest thing the user did; show it rather than the server's copy.
        if (pending is { Dirty: true })
        {
            return ApiResult.Ok(Synthesize(accountId, pending));
        }

        var result = await api.GetProfileAsync(cancellationToken);
        if (result.Value is { } profile)
        {
            await CacheAsync(accountId, profile.DisplayName, profile.Language, dirty: false, cancellationToken);
            return result;
        }

        // Offline with a cached copy: show what we last knew instead of an error (NFR-001 read path).
        if (result.Error?.Failure == ApiFailure.Unreachable && pending is not null)
        {
            return ApiResult.Ok(Synthesize(accountId, pending));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ApiResult<ProfileResponse>> SaveAsync(
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var accountId = await CurrentAccountAsync();
        var result = await api.UpdateProfileAsync(request, cancellationToken);
        if (result.Value is not null)
        {
            await CacheAsync(accountId, request.DisplayName, request.Language, dirty: false, cancellationToken);
            return result;
        }

        // Offline: queue the edit for the next sync (FR-012), and report success so the page applies the
        // language and confirms. Any other failure (a validation refusal) is real and is surfaced — a
        // rejected value must not be queued.
        if (result.Error?.Failure == ApiFailure.Unreachable)
        {
            await CacheAsync(accountId, request.DisplayName, request.Language, dirty: true, cancellationToken);
            return ApiResult.Ok(Synthesize(accountId, new SyncedProfileRecord(
                accountId, request.DisplayName, request.Language, Dirty: true)));
        }

        return result;
    }

    /// <summary>
    /// Flushes a queued offline edit and pulls the account's current preferences (ADR-0035) — run on
    /// each sync trigger alongside the group cycle, so one "Sync now" reconciles everything the account
    /// syncs. Best-effort: an unreachable server or a refused local write leaves the pending edit to be
    /// retried next time, and never derails the group sync.
    /// </summary>
    /// <param name="accountId">The signed-in account to reconcile.</param>
    /// <param name="cancellationToken">Cancels the reconcile.</param>
    /// <returns>A task that completes when the reconcile has run.</returns>
    public async Task ReconcileAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = await PendingForAsync(accountId, cancellationToken);
            if (pending is { Dirty: true })
            {
                // Push the queued edit to the authoritative endpoint. On success clear the pending flag;
                // if it is still unreachable the edit stays queued for the next trigger.
                var pushed = await api.UpdateProfileAsync(
                    new UpdateProfileRequest(pending.DisplayName, pending.Language), cancellationToken);
                if (pushed.Value is not null)
                {
                    await CacheAsync(accountId, pending.DisplayName, pending.Language, dirty: false, cancellationToken);
                }

                return;
            }

            // No local edit pending: adopt the account's current preferences, so a language change made
            // on another device takes effect here (FR-023).
            var current = await api.GetProfileAsync(cancellationToken);
            if (current.Value is { } profile)
            {
                await CacheAsync(accountId, profile.DisplayName, profile.Language, dirty: false, cancellationToken);
                if (!SameCulture(profile.Language, culture.CurrentCulture))
                {
                    await culture.SetCultureAsync(profile.Language, cancellationToken);
                }
            }
        }
        catch (StorageUnavailableException)
        {
            // The local cache could not be written. The authoritative server state is unaffected; the
            // group cycle reports the storage failure. Nothing is lost.
        }
    }

    private async Task<Guid> CurrentAccountAsync()
    {
        var state = await auth.GetAuthenticationStateAsync();
        return state.User.UserId();
    }

    private async Task<SyncedProfileRecord?> PendingForAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var record = await local.LoadProfileAsync(cancellationToken);
        return record?.AccountScope == accountId ? record : null;
    }

    private Task CacheAsync(Guid accountId, string displayName, string language, bool dirty, CancellationToken cancellationToken) =>
        local.SaveProfileAsync(new SyncedProfileRecord(accountId, displayName, language, dirty), cancellationToken);

    // The account address is not part of the profile edit screen (it changes through its own verified
    // flow), so an offline-synthesized response leaves it and the verified flag at neutral values: the
    // page renders only the display name and language.
    private static ProfileResponse Synthesize(Guid accountId, SyncedProfileRecord record) =>
        new(accountId, record.DisplayName, string.Empty, record.Language, EmailVerified: true);

    private static bool SameCulture(string language, CultureInfo current) =>
        string.Equals(AppCultures.Normalize(language), current.Name, StringComparison.OrdinalIgnoreCase);
}
