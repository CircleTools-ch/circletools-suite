using Microsoft.EntityFrameworkCore;
using Suite.Main.Service.Core.Domain;

namespace Suite.Main.Service.Repository;

/// <summary>EF Core database context for the Main module (PostgreSQL adapter).</summary>
/// <param name="options">The context options.</param>
public sealed class MainDbContext(DbContextOptions<MainDbContext> options) : DbContext(options)
{
    /// <summary>Gets the heartbeats table.</summary>
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Heartbeat>(entity =>
        {
            entity.ToTable("heartbeats");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).ValueGeneratedNever();
            entity.Property(h => h.Note).IsRequired().HasMaxLength(500);
            entity.Property(h => h.CreatedAt).IsRequired();
            entity.Ignore(h => h.DomainEvents);
        });
    }
}
