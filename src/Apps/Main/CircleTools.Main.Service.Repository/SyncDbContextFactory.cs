using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection string.
/// Never used at runtime — the host configures the context via DI.
/// </summary>
/// <remarks>
/// It goes through <see cref="SyncRepositoryServiceCollectionExtensions.AddSyncRepository"/> rather
/// than newing the context up, so the migration is scaffolded against exactly the configuration the
/// application runs — including the history table living in the <c>sync</c> schema. A hand-built
/// context here would record its migrations in the wrong place, which shows up as Main's migrations
/// being replayed against a database that already has them.
/// </remarks>
public sealed class SyncDbContextFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    /// <inheritdoc />
    public SyncDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSyncRepository("Host=localhost;Database=maindb;Username=postgres;Password=postgres");

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<SyncDbContext>();
    }
}
