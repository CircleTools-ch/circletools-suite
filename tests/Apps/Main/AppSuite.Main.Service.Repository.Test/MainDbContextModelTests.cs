using AppSuite.Identity;
using AppSuite.Identity.Tokens;
using AppSuite.Main.Service.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Repository.Test;

/// <summary>
/// Model-shape tests. These build the EF model only — no database is contacted — so they run
/// in CI without Postgres and still catch mapping mistakes that would otherwise surface as a
/// broken migration.
/// </summary>
public sealed class MainDbContextModelTests
{
    // Built through DI on purpose: AddAppSuiteIdentityCore is what puts passkey entities in
    // the model. Newing up the context here would test a model the application never uses.
    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<MainDbContext>(o =>
            o.UseNpgsql("Host=localhost;Database=modeltests;Username=u;Password=p"));
        services.AddAppSuiteIdentityCore();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Model_MapsTheIdentityUser()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var user = context.Model.FindEntityType(typeof(AppSuiteUser));

        Assert.NotNull(user);
        Assert.NotNull(user.FindProperty(nameof(AppSuiteUser.DisplayName)));
        Assert.NotNull(user.FindProperty(nameof(AppSuiteUser.PreferredLanguage)));
        Assert.NotNull(user.FindProperty(nameof(AppSuiteUser.CreatedAt)));
    }

    [Fact]
    public void Model_DoesNotMakeDisplayNameUnique()
    {
        // FR-012: two "Anna"s in one family is ordinary.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var user = context.Model.FindEntityType(typeof(AppSuiteUser))!;
        var index = user.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(AppSuiteUser.DisplayName)));

        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_MapsTheRefreshTokenChainWithItsBackingField()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var chain = context.Model.FindEntityType(typeof(RefreshTokenChain));

        Assert.NotNull(chain);
        var tokens = chain.FindNavigation(nameof(RefreshTokenChain.Tokens));
        Assert.NotNull(tokens);
        Assert.Equal("_tokens", tokens.FieldInfo?.Name);
    }

    [Fact]
    public void Model_DoesNotPersistComputedChainState()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var chain = context.Model.FindEntityType(typeof(RefreshTokenChain))!;
        var token = context.Model.FindEntityType(typeof(RefreshToken))!;

        Assert.Null(chain.FindProperty(nameof(RefreshTokenChain.IsRevoked)));
        Assert.Null(chain.FindProperty(nameof(RefreshTokenChain.Current)));
        Assert.Null(token.FindProperty(nameof(RefreshToken.IsConsumed)));
    }

    [Fact]
    public void Model_KeysRefreshTokensByHandle()
    {
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var token = context.Model.FindEntityType(typeof(RefreshToken))!;
        var key = token.FindPrimaryKey();

        Assert.NotNull(key);
        Assert.Equal(nameof(RefreshToken.Handle), Assert.Single(key.Properties).Name);
    }

    [Fact]
    public void Model_MapsPasskeyStorage()
    {
        // FR-010 stores passkeys through ASP.NET Core Identity. If this entity is missing from
        // the model, passkey registration compiles and then fails at runtime with no table.
        using var provider = CreateProvider();
        var context = provider.GetRequiredService<MainDbContext>();

        var passkeyTypes = context.Model.GetEntityTypes()
            .Select(e => e.ClrType.Name)
            .Where(n => n.Contains("Passkey", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(passkeyTypes, n => n.StartsWith("IdentityUserPasskey", StringComparison.Ordinal));
        Assert.Contains(passkeyTypes, n => n.StartsWith("IdentityPasskeyData", StringComparison.Ordinal));
    }
}
