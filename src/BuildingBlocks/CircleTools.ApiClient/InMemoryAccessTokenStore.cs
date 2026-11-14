// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.ApiClient;

/// <summary>
/// The only <see cref="IAccessTokenStore"/> the suite ships: the token lives in a field and dies
/// with the tab (ADR-0021).
/// </summary>
/// <param name="timeProvider">The clock, injected so expiry is testable without waiting.</param>
public sealed class InMemoryAccessTokenStore(TimeProvider timeProvider) : IAccessTokenStore
{
    /// <inheritdoc />
    public string? AccessToken { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <inheritdoc />
    public bool NeedsRefresh(TimeSpan margin) =>
        AccessToken is null || ExpiresAt is null || ExpiresAt.Value - margin <= timeProvider.GetUtcNow();

    /// <inheritdoc />
    public void SetToken(string accessToken, TimeSpan expiresIn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        AccessToken = accessToken;
        ExpiresAt = timeProvider.GetUtcNow() + expiresIn;
    }

    /// <inheritdoc />
    public void Clear()
    {
        AccessToken = null;
        ExpiresAt = null;
    }
}
