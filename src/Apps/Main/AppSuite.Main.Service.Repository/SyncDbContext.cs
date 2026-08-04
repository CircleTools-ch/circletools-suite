using Microsoft.EntityFrameworkCore;

namespace AppSuite.Main.Service.Repository;

/// <summary>
/// EF Core database context for the Platform sync store (FEAT-004, PostgreSQL adapter).
/// </summary>
/// <remarks>
/// <para>
/// Sync lives in the same database as Main and Circles — one deployable service, one connection
/// string (ADR-0003) — but in its <strong>own Postgres schema</strong> (<c>sync</c>) and its own
/// migration history, the same seam Circles keeps. Two contexts sharing a database must not share a
/// migrations-history table.
/// </para>
/// <para>
/// There is deliberately <strong>no foreign key to <c>AspNetUsers</c></strong>. An aggregate holds
/// its account as a <em>value</em>; sync applies the same one-way independence rule Circles follows
/// (ADR-0019, arc42 §5.3), so account deletion is a cleanup the host orchestrates rather than a
/// cascade the database enforces.
/// </para>
/// <para>
/// The context knows nothing about the Secret-Santa schema. A group is a <c>jsonb</c> body it stores
/// and returns opaquely — that is what keeps ADR-0016's "sync sits behind the ports" true at the
/// database as well as in code.
/// </para>
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : DbContext(options)
{
    /// <summary>The Postgres schema every table in this context lives in.</summary>
    public const string Schema = "sync";

    /// <summary>Gets the account-scoped opaque aggregate documents.</summary>
    public DbSet<SyncAggregate> Aggregates => Set<SyncAggregate>();

    /// <summary>Gets the tombstones and un-claim markers.</summary>
    public DbSet<SyncMarker> Markers => Set<SyncMarker>();

    /// <summary>Gets the per-account device registry.</summary>
    public DbSet<SyncDevice> Devices => Set<SyncDevice>();

    /// <summary>Gets the per-account sequence counters.</summary>
    public DbSet<SyncAccountCounter> AccountCounters => Set<SyncAccountCounter>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SyncAggregate>(entity =>
        {
            entity.ToTable("aggregates");
            entity.HasKey(a => new { a.AccountId, a.AggregateId });
            entity.Property(a => a.Kind).IsRequired().HasMaxLength(32);
            entity.Property(a => a.Body).IsRequired().HasColumnType("jsonb");
            entity.Property(a => a.Version).IsRequired();
            entity.Property(a => a.ServerSeq).IsRequired();
            entity.Property(a => a.ServerModifiedAt).IsRequired();

            // Every pull is "my account's rows above my cursor", newest last.
            entity.HasIndex(a => new { a.AccountId, a.ServerSeq });
        });

        modelBuilder.Entity<SyncMarker>(entity =>
        {
            entity.ToTable("markers");
            entity.HasKey(m => new { m.AccountId, m.AggregateId });
            entity.Property(m => m.Kind).IsRequired().HasMaxLength(32);
            entity.Property(m => m.Version).IsRequired();
            entity.Property(m => m.ServerSeq).IsRequired();
            entity.Property(m => m.MarkerType).IsRequired().HasConversion<string>().HasMaxLength(16);

            // Markers ride the same "above my cursor" pull, and retention sweeps by sequence.
            entity.HasIndex(m => new { m.AccountId, m.ServerSeq });
        });

        modelBuilder.Entity<SyncDevice>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(d => new { d.AccountId, d.DeviceId });
            entity.Property(d => d.DeviceId).IsRequired().HasMaxLength(128);
            entity.Property(d => d.LastCursor).IsRequired();
            entity.Property(d => d.LastSeenAt).IsRequired();
        });

        modelBuilder.Entity<SyncAccountCounter>(entity =>
        {
            entity.ToTable("account_counters");
            entity.HasKey(c => c.AccountId);
            entity.Property(c => c.NextSeq).IsRequired();
        });
    }
}
