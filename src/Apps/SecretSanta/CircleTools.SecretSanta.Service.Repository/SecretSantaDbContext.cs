using Microsoft.EntityFrameworkCore;

namespace CircleTools.SecretSanta.Service.Repository;

/// <summary>
/// The Secret-Santa group store (ADR-0086): one row per online group, its role assignments, its join link, its
/// members' own entries and its reveal tokens. References to accounts are by value; there is no foreign key into
/// the platform's schemas.
/// </summary>
/// <param name="options">The context options.</param>
public sealed class SecretSantaDbContext(DbContextOptions<SecretSantaDbContext> options) : DbContext(options)
{
    /// <summary>The schema all tables live in.</summary>
    public const string Schema = "secretsanta";

    /// <summary>Gets the groups.</summary>
    public DbSet<GroupRow> Groups => Set<GroupRow>();

    /// <summary>Gets the role assignments.</summary>
    public DbSet<RoleAssignmentRow> RoleAssignments => Set<RoleAssignmentRow>();

    /// <summary>Gets the join links.</summary>
    public DbSet<JoinLinkRow> JoinLinks => Set<JoinLinkRow>();

    /// <summary>Gets the members' own entries.</summary>
    public DbSet<MemberEntryRow> MemberEntries => Set<MemberEntryRow>();

    /// <summary>Gets the reveal tokens.</summary>
    public DbSet<RevealTokenRow> RevealTokens => Set<RevealTokenRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<GroupRow>(entity =>
        {
            entity.ToTable("groups");
            entity.HasKey(g => g.GroupId);
            entity.Property(g => g.Body).IsRequired().HasColumnType("jsonb");
            entity.Property(g => g.CreatorKeyHash).HasMaxLength(64);

            // The retention sweep (NFR-010) scans by last activity.
            entity.HasIndex(g => g.LastActivityAt);
        });

        modelBuilder.Entity<RoleAssignmentRow>(entity =>
        {
            entity.ToTable("role_assignments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Role).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.PrincipalKind).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.State).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.Route).HasConversion<string>().HasMaxLength(16);
            entity.Property(a => a.SecretHash).HasMaxLength(64);
            entity.Property(a => a.SecretCipher).HasMaxLength(128);

            entity.HasIndex(a => a.GroupId);

            // One role per account per group; one principal per member; one assignment per secret.
            entity.HasIndex(a => new { a.GroupId, a.AccountId }).IsUnique().HasFilter("\"AccountId\" IS NOT NULL");
            entity.HasIndex(a => new { a.GroupId, a.MemberId }).IsUnique().HasFilter("\"MemberId\" IS NOT NULL");
            entity.HasIndex(a => a.SecretHash).IsUnique().HasFilter("\"SecretHash\" IS NOT NULL");

            // "Every group this account has a role in" (overview, account deletion).
            entity.HasIndex(a => a.AccountId);
        });

        modelBuilder.Entity<JoinLinkRow>(entity =>
        {
            entity.ToTable("join_links");
            entity.HasKey(l => l.TokenHash);
            entity.Property(l => l.TokenHash).HasMaxLength(64);
            entity.Property(l => l.TokenCipher).IsRequired().HasMaxLength(128);
            entity.HasIndex(l => l.GroupId);
        });

        modelBuilder.Entity<MemberEntryRow>(entity =>
        {
            entity.ToTable("member_entries");
            entity.HasKey(e => new { e.GroupId, e.MemberId });
            entity.Property(e => e.GiftNote).HasMaxLength(500);
            entity.Property(e => e.Wishlists).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.KeptBy).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<RevealTokenRow>(entity =>
        {
            entity.ToTable("reveal_tokens");
            entity.HasKey(t => t.TokenHash);
            entity.Property(t => t.TokenHash).HasMaxLength(64);
            entity.Property(t => t.TokenCipher).IsRequired().HasMaxLength(128);

            // A giver has one stable token per event (ADR-0068): re-draws keep it.
            entity.HasIndex(t => new { t.GroupId, t.EventId, t.GiverMemberId }).IsUnique();
        });
    }
}
