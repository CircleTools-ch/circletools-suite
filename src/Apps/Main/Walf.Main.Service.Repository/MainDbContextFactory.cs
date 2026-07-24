using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Walf.Main.Service.Repository;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection
/// string. Never used at runtime — the host configures the context via DI.
/// </summary>
public sealed class MainDbContextFactory : IDesignTimeDbContextFactory<MainDbContext>
{
    /// <inheritdoc />
    public MainDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MainDbContext>()
            .UseNpgsql("Host=localhost;Database=maindb;Username=postgres;Password=postgres")
            .Options;

        return new MainDbContext(options);
    }
}
