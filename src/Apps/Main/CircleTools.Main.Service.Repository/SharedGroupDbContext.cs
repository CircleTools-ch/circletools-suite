using Microsoft.EntityFrameworkCore;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// EF Core database context for the account-free, shared-by-link storage tier (ADR-0058, FEAT-013).
/// </summary>
/// <remarks>
/// A fifth context and its own Postgres schema (<c>shared</c>) in the same database, on the same seam
/// as <c>sync</c>, <c>linking</c> and <c>circles</c> (ADR-0003) — its own migration history so it
/// stays independent of the others. Like the sync store there is no foreign key to Identity: a group's
/// optional owning account is a value, and account deletion is a cleanup the host orchestrates. The
/// context does not understand the Secret-Santa schema; a group is a <c>jsonb</c> body stored and
/// returned opaquely.
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class SharedGroupDbContext(DbContextOptions<SharedGroupDbContext> options) : DbContext(options)
{
    /// <summary>The Postgres schema every table in this context lives in.</summary>
    public const string Schema = "shared";

    /// <summary>Gets the shared-by-link group documents.</summary>
    public DbSet<SharedGroupRow> Groups => Set<SharedGroupRow>();

    /// <summary>Gets the share tokens addressing the groups.</summary>
    public DbSet<SharedGroupToken> Tokens => Set<SharedGroupToken>();

    /// <summary>Gets the per-member person-claims that lock which participant is whom.</summary>
    public DbSet<SharedMemberClaim> MemberClaims => Set<SharedMemberClaim>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<SharedGroupRow>(entity =>
        {
            entity.ToTable("groups");
            entity.HasKey(g => g.GroupId);
            entity.Property(g => g.Body).IsRequired().HasColumnType("jsonb");
            entity.Property(g => g.Version).IsRequired();
            entity.Property(g => g.ServerModifiedAt).IsRequired();
            entity.Property(g => g.AdminClaimedByName).HasMaxLength(128);
            entity.Property(g => g.CreatedAt).IsRequired();
            entity.Property(g => g.LastActivityAt).IsRequired();

            // The retention sweep (NFR-010) scans by last activity.
            entity.HasIndex(g => g.LastActivityAt);
        });

        modelBuilder.Entity<SharedGroupToken>(entity =>
        {
            entity.ToTable("tokens");
            entity.HasKey(t => t.Token);
            entity.Property(t => t.Token).HasMaxLength(64);
            entity.Property(t => t.Capability).IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(t => t.Active).IsRequired();
            entity.Property(t => t.RetiredByClaim).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();

            // Resolve every token for a group (rotate, delete, claim-admin).
            entity.HasIndex(t => t.GroupId);
        });

        modelBuilder.Entity<SharedMemberClaim>(entity =>
        {
            entity.ToTable("member_claims");
            entity.HasKey(c => new { c.GroupId, c.MemberId });
            entity.Property(c => c.ClaimKind).IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(c => c.DeviceHandle).HasMaxLength(128);
            entity.Property(c => c.ClaimedAt).IsRequired();
        });
    }
}
