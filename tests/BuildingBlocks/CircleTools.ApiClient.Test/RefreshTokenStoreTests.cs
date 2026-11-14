// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.ApiClient.Test.TestSupport;
using CircleTools.Ui.Abstractions;

namespace CircleTools.ApiClient.Test;

/// <summary>
/// Tests for where the refresh token is kept (ADR-0026): durable when the user asked to stay signed
/// in, session-only otherwise.
/// </summary>
public sealed class RefreshTokenStoreTests
{
    private const string Key = "test.refreshToken";

    [Fact]
    public async Task StaySignedIn_StoresDurablyAndClearsTheSessionCopy()
    {
        var (store, backing) = NewStore();
        await backing.SetAsync(StorageScope.Session, Key, "old");

        await store.SaveAsync("new", staySignedIn: true);

        Assert.Equal("new", backing.Peek(StorageScope.Durable, Key));
        Assert.Null(backing.Peek(StorageScope.Session, Key));
    }

    [Fact]
    public async Task WithoutStaySignedIn_StoresForTheSessionAndClearsTheDurableCopy()
    {
        var (store, backing) = NewStore();
        await backing.SetAsync(StorageScope.Durable, Key, "old");

        await store.SaveAsync("new", staySignedIn: false);

        // Without the clear, the durable token from an earlier "stay signed in" would keep winning
        // the read and quietly defeat the user's newer choice.
        Assert.Equal("new", backing.Peek(StorageScope.Session, Key));
        Assert.Null(backing.Peek(StorageScope.Durable, Key));
    }

    [Fact]
    public async Task Read_PrefersTheDurableToken()
    {
        var (store, backing) = NewStore();
        await backing.SetAsync(StorageScope.Session, Key, "session");
        await backing.SetAsync(StorageScope.Durable, Key, "durable");

        Assert.Equal("durable", await store.ReadAsync());
    }

    [Theory]
    [InlineData(StorageScope.Durable)]
    [InlineData(StorageScope.Session)]
    public async Task Renew_KeepsTheTokenInTheScopeItWasAlreadyIn(StorageScope scope)
    {
        var (store, backing) = NewStore();
        await backing.SetAsync(scope, Key, "first");

        await store.RenewAsync("rotated");

        Assert.Equal("rotated", backing.Peek(scope, Key));
        Assert.Null(backing.Peek(Other(scope), Key));
    }

    [Fact]
    public async Task Renew_WithNothingStored_FallsBackToTheSessionScope()
    {
        var (store, backing) = NewStore();

        await store.RenewAsync("rotated");

        // Forgetting too soon is recoverable with a sign-in; remembering too long is not what the
        // user asked for.
        Assert.Equal("rotated", backing.Peek(StorageScope.Session, Key));
        Assert.Null(backing.Peek(StorageScope.Durable, Key));
    }

    [Fact]
    public async Task Clear_EmptiesBothScopes()
    {
        var (store, backing) = NewStore();
        await backing.SetAsync(StorageScope.Durable, Key, "durable");
        await backing.SetAsync(StorageScope.Session, Key, "session");

        await store.ClearAsync();

        Assert.Null(await store.ReadAsync());
        Assert.Null(backing.Peek(StorageScope.Durable, Key));
        Assert.Null(backing.Peek(StorageScope.Session, Key));
    }

    [Theory]
    [InlineData(true, StorageScope.Durable)]
    [InlineData(false, StorageScope.Session)]
    public async Task TheAccount_IsRememberedInTheTokensScope(bool staySignedIn, StorageScope scope)
    {
        var (store, backing) = NewStore();
        var account = Guid.NewGuid();
        await store.SaveAsync("token", staySignedIn);

        await store.RememberAccountAsync(account);

        Assert.Equal(account, await store.ReadAccountAsync());
        Assert.Equal(account.ToString(), backing.Peek(scope, Key + ".account"));
        Assert.Null(backing.Peek(Other(scope), Key + ".account"));
    }

    [Fact]
    public async Task TheAccount_GoesWithTheToken()
    {
        var (store, _) = NewStore();
        await store.SaveAsync("token", staySignedIn: true);
        await store.RememberAccountAsync(Guid.NewGuid());

        await store.ClearAsync();

        // No remembered session, no account to show data for.
        Assert.Null(await store.ReadAccountAsync());
    }

    private static StorageScope Other(StorageScope scope) =>
        scope == StorageScope.Durable ? StorageScope.Session : StorageScope.Durable;

    private static (IRefreshTokenStore Store, FakeKeyValueStore Backing) NewStore()
    {
        var backing = new FakeKeyValueStore();
        return (new RefreshTokenStore(backing, new RefreshTokenStoreOptions(Key)), backing);
    }
}
