using AppSuite.Identity.Tokens;
using Microsoft.Extensions.Options;

namespace AppSuite.Identity.Test;

/// <summary>An in-memory <see cref="IRefreshTokenChainStore"/> for session-service tests.</summary>
internal sealed class FakeChainStore : IRefreshTokenChainStore
{
    private readonly List<RefreshTokenChain> _chains = [];

    public int SaveCount { get; private set; }

    public IReadOnlyList<RefreshTokenChain> Chains => _chains;

    public Task<RefreshTokenChain?> FindByTokenHandleAsync(
        string handle,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_chains.SingleOrDefault(c =>
            c.Tokens.Any(t => string.Equals(t.Handle, handle, StringComparison.Ordinal))));

    public Task AddAsync(RefreshTokenChain chain, CancellationToken cancellationToken = default)
    {
        _chains.Add(chain);
        return Task.CompletedTask;
    }

    public Task<int> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var live = _chains.Where(c => c.UserId == userId && !c.IsRevoked).ToList();
        foreach (var chain in live)
        {
            chain.Revoke(now);
        }

        return Task.FromResult(live.Count);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>A user lookup backed by a dictionary, so accounts can be removed mid-test.</summary>
internal sealed class FakeUserLookup : ISessionUserLookup
{
    private readonly Dictionary<Guid, AppSuiteUser> _users = [];

    public void Add(AppSuiteUser user) => _users[user.Id] = user;

    public void Remove(Guid userId) => _users.Remove(userId);

    public Task<AppSuiteUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.GetValueOrDefault(userId));
}

/// <summary>A clock the tests move by hand.</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public void Advance(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Shared construction helpers.</summary>
internal static class TestFactory
{
    public const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

    public static AppSuiteUser User(bool emailConfirmed = true) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = "anna@example.test",
        UserName = "anna@example.test",
        DisplayName = "Anna",
        PreferredLanguage = "de-CH",
        EmailConfirmed = emailConfirmed,
        CreatedAt = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.Zero),
    };

    public static AccessTokenIssuer Issuer(TimeProvider clock, string? signingKey = null) =>
        new(
            Options.Create(new JwtOptions { SigningKey = signingKey ?? SigningKey }),
            Options.Create(new TokenLifetimeOptions()),
            clock);

    public static SessionService Session(
        IRefreshTokenChainStore chains,
        ISessionUserLookup users,
        TimeProvider clock) =>
        new(chains, users, Issuer(clock), Options.Create(new TokenLifetimeOptions()), clock);
}
