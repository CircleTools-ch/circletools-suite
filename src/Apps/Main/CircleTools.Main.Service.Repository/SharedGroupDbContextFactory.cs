using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection string. Never
/// used at runtime. It goes through
/// <see cref="SharedGroupRepositoryServiceCollectionExtensions.AddSharedGroupsRepository"/> so the
/// migration is scaffolded against exactly the runtime configuration — including the history table
/// living in the <c>shared</c> schema.
/// </summary>
public sealed class SharedGroupDbContextFactory : IDesignTimeDbContextFactory<SharedGroupDbContext>
{
    /// <inheritdoc />
    public SharedGroupDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSharedGroupsRepository("Host=localhost;Database=maindb;Username=postgres;Password=postgres");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<SharedGroupDbContext>();
    }
}
