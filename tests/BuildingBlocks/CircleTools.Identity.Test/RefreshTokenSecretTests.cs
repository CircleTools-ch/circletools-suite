using CircleTools.Identity.Tokens;

namespace CircleTools.Identity.Test;

/// <summary>
/// Covers the secret/handle split: the client holds the secret, the database holds only a hash.
/// </summary>
public sealed class RefreshTokenSecretTests
{
    [Fact]
    public void Create_ReturnsAHandleThatIsNotTheSecret()
    {
        var (secret, handle) = RefreshTokenSecret.Create();

        Assert.NotEqual(secret, handle);
        Assert.NotEmpty(secret);
        Assert.NotEmpty(handle);
    }

    [Fact]
    public void Create_IsUnpredictable()
    {
        var secrets = Enumerable.Range(0, 500).Select(_ => RefreshTokenSecret.Create().Secret).ToList();

        Assert.Equal(secrets.Count, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void HandleFor_IsDeterministic_SoLookupByHandleWorks()
    {
        var (secret, handle) = RefreshTokenSecret.Create();

        Assert.Equal(handle, RefreshTokenSecret.HandleFor(secret));
        Assert.Equal(handle, RefreshTokenSecret.HandleFor(secret));
    }

    [Fact]
    public void HandleFor_DiffersPerSecret()
    {
        var first = RefreshTokenSecret.Create();
        var second = RefreshTokenSecret.Create();

        Assert.NotEqual(first.Handle, second.Handle);
    }

    [Fact]
    public void Handle_FitsTheStoredColumn()
    {
        // refresh_tokens.Handle is varchar(128); a longer handle would truncate or throw.
        var (_, handle) = RefreshTokenSecret.Create();

        Assert.True(handle.Length <= 128, $"handle was {handle.Length} characters");
    }

    [Fact]
    public void HandleFor_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => RefreshTokenSecret.HandleFor(""));
        Assert.Throws<ArgumentNullException>(() => RefreshTokenSecret.HandleFor(null!));
    }
}
