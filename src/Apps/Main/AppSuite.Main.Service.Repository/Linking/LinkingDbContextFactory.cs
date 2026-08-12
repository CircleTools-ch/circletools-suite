using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace AppSuite.Main.Service.Repository.Linking;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection string. Never
/// used at runtime — the host configures the context via DI. It goes through
/// <see cref="LinkingRepositoryServiceCollectionExtensions.AddLinkingRepository"/> so the migration is
/// scaffolded against exactly the configuration the application runs, including the history table in
/// the <c>linking</c> schema.
/// </summary>
public sealed class LinkingDbContextFactory : IDesignTimeDbContextFactory<LinkingDbContext>
{
    /// <inheritdoc />
    public LinkingDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLinkingRepository("Host=localhost;Database=maindb;Username=postgres;Password=postgres");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<LinkingDbContext>();
    }
}
