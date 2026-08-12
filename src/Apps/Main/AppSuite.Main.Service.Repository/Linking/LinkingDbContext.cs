using AppSuite.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AppSuite.Main.Service.Repository.Linking;

/// <summary>
/// EF Core context for the M4 linking surface (FEAT-005) — the one server-side store ADR-0020
/// sanctioned. Its own Postgres schema (<c>linking</c>) and its own migration history, the same seam
/// sync and Circles keep. There is no foreign key to Identity: every account is held as a value, so
/// account deletion is a host-orchestrated cleanup, not a database cascade (ADR-0019).
/// </summary>
/// <remarks>
/// Unlike the generic sync store, this context <em>does</em> understand the Secret-Santa shape of an
/// assignment — that is the deliberate, ADR-0020-authorized exception. The generic sync store stays
/// opaque and untouched.
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class LinkingDbContext(DbContextOptions<LinkingDbContext> options) : DbContext(options)
{
    /// <summary>The Postgres schema every table in this context lives in.</summary>
    public const string Schema = "linking";

    /// <summary>Gets the member-link requests.</summary>
    public DbSet<MemberLinkRow> MemberLinks => Set<MemberLinkRow>();

    /// <summary>Gets the per-user assignment projection rows.</summary>
    public DbSet<AssignmentRow> Assignments => Set<AssignmentRow>();

    /// <summary>Gets linked members' self-owned wishlists.</summary>
    public DbSet<MemberWishlistRow> MemberWishlists => Set<MemberWishlistRow>();

    /// <summary>Gets the pending link resolutions the organizer's client drains.</summary>
    public DbSet<LinkResolutionRow> Resolutions => Set<LinkResolutionRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<MemberLinkRow>(entity =>
        {
            entity.ToTable("member_links");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.GroupName).IsRequired().HasMaxLength(200);
            entity.Property(m => m.MemberDisplayName).IsRequired().HasMaxLength(200);
            entity.Property(m => m.State).IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.CreatedAt).IsRequired();
            entity.Property(m => m.ExpiresAt).IsRequired();

            // A recipient reads "the pending requests addressed to me"; the organizer reads "the links I own".
            entity.HasIndex(m => new { m.TargetAccountId, m.State });
            entity.HasIndex(m => new { m.OrganizerAccountId, m.GroupId });
            // One user backs at most one member per group: enforced in the store, indexed here.
            entity.HasIndex(m => new { m.GroupId, m.TargetAccountId, m.State });
        });

        modelBuilder.Entity<AssignmentRow>(entity =>
        {
            entity.ToTable("assignments");
            // The authorized read is a single row lookup by (group, giver account).
            entity.HasKey(a => new { a.GroupId, a.GiverAccountId });
            entity.Property(a => a.GroupName).IsRequired().HasMaxLength(200);
            entity.Property(a => a.RecipientName).IsRequired().HasMaxLength(200);
            entity.Property(a => a.RecipientWishlistSnapshot).IsRequired().HasColumnType("jsonb");
            entity.Property(a => a.BudgetCurrency).HasMaxLength(3);
            entity.Property(a => a.EventLocation).HasMaxLength(400);
            entity.Property(a => a.EventDescription).HasMaxLength(2000);
            entity.Property(a => a.DrawVersion).IsRequired();

            // "All my assignments across groups" and the recipient-wishlist composition by member.
            entity.HasIndex(a => a.GiverAccountId);
            entity.HasIndex(a => new { a.GroupId, a.RecipientMemberId });
        });

        modelBuilder.Entity<MemberWishlistRow>(entity =>
        {
            entity.ToTable("member_wishlists");
            entity.HasKey(w => new { w.GroupId, w.MemberAccountId });
            entity.Property(w => w.GroupName).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Items).IsRequired().HasColumnType("jsonb");
            entity.Property(w => w.Version).IsRequired();

            entity.HasIndex(w => w.MemberAccountId);
        });

        modelBuilder.Entity<LinkResolutionRow>(entity =>
        {
            entity.ToTable("resolutions");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Kind).IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(r => r.CreatedAt).IsRequired();

            entity.HasIndex(r => new { r.OrganizerAccountId, r.CreatedAt });
        });
    }
}
