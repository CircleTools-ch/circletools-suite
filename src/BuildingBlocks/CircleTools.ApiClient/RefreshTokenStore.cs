// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.ApiClient;

/// <summary>Where the refresh token is remembered.</summary>
/// <param name="StorageKey">The key the refresh token is stored under.</param>
public sealed record RefreshTokenStoreOptions(string StorageKey);

/// <summary>
/// Stores the refresh token in one of the two <see cref="StorageScope"/>s, according to whether the
/// user asked to stay signed in (ADR-0026).
/// </summary>
/// <param name="store">The host's key/value storage.</param>
/// <param name="options">The key to store under.</param>
public sealed class RefreshTokenStore(IKeyValueStore store, RefreshTokenStoreOptions options) : IRefreshTokenStore
{
    /// <inheritdoc />
    public async ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var durable = await store.GetAsync(StorageScope.Durable, options.StorageKey, cancellationToken);
        if (!string.IsNullOrEmpty(durable))
        {
            return durable;
        }

        var session = await store.GetAsync(StorageScope.Session, options.StorageKey, cancellationToken);
        return string.IsNullOrEmpty(session) ? null : session;
    }

    /// <summary>
    /// Writes the token to the chosen scope and removes it from the other one.
    /// </summary>
    /// <remarks>
    /// Clearing the other scope is the point: without it, signing in once with "stay signed in" and
    /// again without it would leave a durable token behind that
    /// <see cref="ReadAsync"/> would keep preferring, quietly defeating the choice.
    /// </remarks>
    /// <inheritdoc />
    public async ValueTask SaveAsync(
        string refreshToken,
        bool staySignedIn,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var (target, other) = staySignedIn
            ? (StorageScope.Durable, StorageScope.Session)
            : (StorageScope.Session, StorageScope.Durable);

        await store.RemoveAsync(other, options.StorageKey, cancellationToken);

        // Ignored on purpose: a refresh token that could not be stored costs a re-login, which is
        // the same outcome the user gets in a browser that blocks storage outright.
        _ = await store.SetAsync(target, options.StorageKey, refreshToken, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RenewAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        // Whichever scope holds the outgoing token is the scope the user chose. When neither does —
        // the tab was signed in before this build, or storage is unavailable — Session is the
        // conservative answer: it forgets too soon rather than remembering too long.
        var durable = await store.GetAsync(StorageScope.Durable, options.StorageKey, cancellationToken);
        var scope = string.IsNullOrEmpty(durable) ? StorageScope.Session : StorageScope.Durable;

        _ = await store.SetAsync(scope, options.StorageKey, refreshToken, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RememberAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        // Next to the token, in its scope: a session-only sign-in must not leave the account behind for good.
        var durable = await store.GetAsync(StorageScope.Durable, options.StorageKey, cancellationToken);
        var (target, other) = string.IsNullOrEmpty(durable)
            ? (StorageScope.Session, StorageScope.Durable)
            : (StorageScope.Durable, StorageScope.Session);

        await store.RemoveAsync(other, AccountKey, cancellationToken);
        _ = await store.SetAsync(target, AccountKey, accountId.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<Guid?> ReadAccountAsync(CancellationToken cancellationToken = default)
    {
        if (await ReadAsync(cancellationToken) is null)
        {
            return null;
        }

        var value = await store.GetAsync(StorageScope.Durable, AccountKey, cancellationToken)
            ?? await store.GetAsync(StorageScope.Session, AccountKey, cancellationToken);
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }

    /// <inheritdoc />
    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        await store.RemoveAsync(StorageScope.Durable, options.StorageKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Session, options.StorageKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Durable, AccountKey, cancellationToken);
        await store.RemoveAsync(StorageScope.Session, AccountKey, cancellationToken);
    }

    private string AccountKey => options.StorageKey + ".account";
}
