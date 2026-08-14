using System.Collections.Concurrent;

namespace CircleTools.Identity.Tokens;

/// <summary>
/// An in-process, single-use store for SSO authorization codes (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// The codes live seconds and are redeemed on the same host that minted them, so a durable relational
/// table would be overkill (the feature spec allows either). <see cref="ConsumeAsync"/> removes
/// atomically, so a racing double-redemption cannot both succeed. Expired entries are swept lazily on
/// access; a single service handles the seasonal, single-instance load this suite runs at
/// (ADR-0036). A multi-instance deployment would swap this for a shared, equally single-use store —
/// the interface is the seam.
/// </remarks>
/// <param name="clock">Time source, injected so expiry is testable.</param>
public sealed class InMemoryConnectAuthorizationCodeStore(TimeProvider clock) : IConnectAuthorizationCodeStore
{
    private readonly ConcurrentDictionary<string, ConnectAuthorizationCode> _codes = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask AddAsync(ConnectAuthorizationCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        SweepExpired();
        _codes[code.Handle] = code;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<ConnectAuthorizationCode?> ConsumeAsync(string handle, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        SweepExpired();
        return ValueTask.FromResult(_codes.TryRemove(handle, out var code) ? code : null);
    }

    private void SweepExpired()
    {
        var now = clock.GetUtcNow();

        foreach (var (handle, code) in _codes)
        {
            if (code.ExpiresAt <= now)
            {
                _codes.TryRemove(handle, out _);
            }
        }
    }
}
