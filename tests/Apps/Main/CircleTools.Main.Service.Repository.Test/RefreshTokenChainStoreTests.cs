using CircleTools.Identity.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository.Test;

/// <summary>
/// Integration coverage for the refresh-chain rows of TP-002, against a real Postgres.
/// </summary>
/// <remarks>
/// The unit tests in <c>CircleTools.Identity.Test</c> prove the rotation <em>rules</em>. These prove
/// the rules still hold once the aggregate has been through the database — consumption state and
/// the successor link have to survive a round-trip, or reuse detection silently stops working
/// while every unit test stays green.
/// </remarks>
[Collection(SharedPostgresDatabase.CollectionName)]
public sealed class RefreshTokenChainStoreTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    private static TokenLifetimeOptions Options() => new();

    private async Task<T> WithStoreAsync<T>(Func<IRefreshTokenChainStore, Task<T>> action)
    {
        await using var provider = fixture.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IRefreshTokenChainStore>();
        return await action(store);
    }

    private async Task WithStoreAsync(Func<IRefreshTokenChainStore, Task> action) =>
        await WithStoreAsync<object?>(async s =>
        {
            await action(s);
            return null;
        });

    [Fact]
    public async Task Chain_RoundTripsThroughTheDatabase()
    {
        var userId = Guid.CreateVersion7();
        var (secret, handle) = RefreshTokenSecret.Create();

        await WithStoreAsync(async store =>
        {
            await store.AddAsync(RefreshTokenChain.Start(userId, handle, Now, Options()));
            await store.SaveChangesAsync();
        });

        var loaded = await WithStoreAsync(store =>
            store.FindByTokenHandleAsync(RefreshTokenSecret.HandleFor(secret)));

        Assert.NotNull(loaded);
        Assert.Equal(userId, loaded.UserId);
        Assert.False(loaded.IsRevoked);
        Assert.Equal(handle, Assert.Single(loaded.Tokens).Handle);
    }

    [Fact]
    public async Task Rotation_PersistsConsumptionAndSuccessor()
    {
        var userId = Guid.CreateVersion7();
        var (_, first) = RefreshTokenSecret.Create();
        var (_, second) = RefreshTokenSecret.Create();

        await WithStoreAsync(async store =>
        {
            await store.AddAsync(RefreshTokenChain.Start(userId, first, Now, Options()));
            await store.SaveChangesAsync();
        });

        await WithStoreAsync(async store =>
        {
            var chain = await store.FindByTokenHandleAsync(first);
            var result = chain!.Redeem(first, second, "secret-2", Now.AddMinutes(10), Options());
            Assert.IsType<RefreshTokenRedemption.Rotated>(result);
            await store.SaveChangesAsync();
        });

        // Reload in a fresh context: the consumed flag and successor link must have survived.
        var reloaded = await WithStoreAsync(store => store.FindByTokenHandleAsync(second));

        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Tokens.Count);
        var original = reloaded.Tokens.Single(t => t.Handle == first);
        Assert.True(original.IsConsumed);
        Assert.Equal(second, original.SuccessorHandle);
        Assert.Equal(second, reloaded.Current.Handle);
    }

    [Fact]
    public async Task ReuseAcrossRequests_IsDetectedAndRevokesTheChain()
    {
        // The case that matters: each redemption is a separate request with its own context, so
        // detection has to work from persisted state alone.
        var userId = Guid.CreateVersion7();
        var (_, first) = RefreshTokenSecret.Create();
        var (_, second) = RefreshTokenSecret.Create();
        var (_, third) = RefreshTokenSecret.Create();

        await WithStoreAsync(async store =>
        {
            await store.AddAsync(RefreshTokenChain.Start(userId, first, Now, Options()));
            await store.SaveChangesAsync();
        });

        await WithStoreAsync(async store =>
        {
            var chain = await store.FindByTokenHandleAsync(first);
            chain!.Redeem(first, second, "secret-2", Now, Options());
            await store.SaveChangesAsync();
        });

        await WithStoreAsync(async store =>
        {
            var chain = await store.FindByTokenHandleAsync(first);
            var replayAt = Now + Options().RefreshReplayGraceWindow + TimeSpan.FromMinutes(1);
            var result = chain!.Redeem(first, third, "secret-3", replayAt, Options());

            var rejected = Assert.IsType<RefreshTokenRedemption.Rejected>(result);
            Assert.Equal(RefreshTokenRejection.ReuseDetected, rejected.Reason);
            await store.SaveChangesAsync();
        });

        var afterTheft = await WithStoreAsync(store => store.FindByTokenHandleAsync(second));

        Assert.NotNull(afterTheft);
        Assert.True(afterTheft.IsRevoked);
    }

    [Fact]
    public async Task RevokeAllForUser_EndsEverySessionButLeavesOtherUsersAlone()
    {
        var victim = Guid.CreateVersion7();
        var bystander = Guid.CreateVersion7();
        var (_, a) = RefreshTokenSecret.Create();
        var (_, b) = RefreshTokenSecret.Create();
        var (_, c) = RefreshTokenSecret.Create();

        await WithStoreAsync(async store =>
        {
            await store.AddAsync(RefreshTokenChain.Start(victim, a, Now, Options()));
            await store.AddAsync(RefreshTokenChain.Start(victim, b, Now, Options()));
            await store.AddAsync(RefreshTokenChain.Start(bystander, c, Now, Options()));
            await store.SaveChangesAsync();
        });

        var revoked = await WithStoreAsync(async store =>
        {
            var count = await store.RevokeAllForUserAsync(victim, Now.AddHours(1));
            await store.SaveChangesAsync();
            return count;
        });

        Assert.Equal(2, revoked);

        var first = await WithStoreAsync(store => store.FindByTokenHandleAsync(a));
        var second = await WithStoreAsync(store => store.FindByTokenHandleAsync(b));
        var other = await WithStoreAsync(store => store.FindByTokenHandleAsync(c));

        Assert.True(first!.IsRevoked);
        Assert.True(second!.IsRevoked);
        Assert.False(other!.IsRevoked);
    }

    [Fact]
    public async Task ConcurrentRefreshAcrossRequests_ReturnsTheSameUsableSecret()
    {
        // The racing-refresh case as it actually happens: two tabs, two requests, two contexts.
        // The second must receive a secret it can really use — which is only possible because the
        // successor's secret is retained for the grace window. Handles are hashes; if this
        // returned a handle the second tab would be holding an unusable string.
        var userId = Guid.CreateVersion7();
        var (_, first) = RefreshTokenSecret.Create();
        var (secondSecret, secondHandle) = RefreshTokenSecret.Create();
        var (_, thirdHandle) = RefreshTokenSecret.Create();

        await WithStoreAsync(async store =>
        {
            await store.AddAsync(RefreshTokenChain.Start(userId, first, Now, Options()));
            await store.SaveChangesAsync();
        });

        await WithStoreAsync(async store =>
        {
            var chain = await store.FindByTokenHandleAsync(first);
            chain!.Redeem(first, secondHandle, secondSecret, Now, Options());
            await store.SaveChangesAsync();
        });

        var replayed = await WithStoreAsync(async store =>
        {
            var chain = await store.FindByTokenHandleAsync(first);
            var result = chain!.Redeem(first, thirdHandle, "unused", Now.AddSeconds(2), Options());
            await store.SaveChangesAsync();
            return Assert.IsType<RefreshTokenRedemption.ReplayedWithinGrace>(result);
        });

        Assert.Equal(secondSecret, replayed.Secret);

        // And the secret really works: hashing it finds the same live chain.
        var found = await WithStoreAsync(store =>
            store.FindByTokenHandleAsync(RefreshTokenSecret.HandleFor(replayed.Secret)));

        Assert.NotNull(found);
        Assert.False(found.IsRevoked);
    }

    [Fact]
    public async Task FindByTokenHandle_ReturnsNullForAnUnknownHandle()
    {
        var (_, unknown) = RefreshTokenSecret.Create();

        var chain = await WithStoreAsync(store => store.FindByTokenHandleAsync(unknown));

        Assert.Null(chain);
    }
}
