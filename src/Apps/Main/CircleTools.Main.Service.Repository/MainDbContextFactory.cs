using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Design-time factory used by the EF Core tools (migrations) with a dummy connection
/// string. Never used at runtime — the host configures the context via DI.
/// </summary>
/// <remarks>
/// It builds a real service provider rather than newing up the context, because
/// <see cref="MainRepositoryServiceCollectionExtensions.AddCircleToolsIdentityCore"/> is what
/// decides whether passkey entities are in the model. A hand-constructed context here would
/// scaffold migrations against a model the application never uses — a divergence that shows up
/// only as a missing table at runtime.
/// </remarks>
public sealed class MainDbContextFactory : IDesignTimeDbContextFactory<MainDbContext>
{
    /// <inheritdoc />
    public MainDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MainDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=maindb;Username=postgres;Password=postgres"));
        services.AddCircleToolsIdentityCore();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<MainDbContext>();
    }
}
