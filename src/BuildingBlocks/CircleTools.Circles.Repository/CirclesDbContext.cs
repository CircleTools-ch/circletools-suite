using CircleTools.Circles.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace CircleTools.Circles.Repository;

/// <summary>
/// EF Core database context for the Circles bounded context (PostgreSQL adapter).
/// </summary>
/// <remarks>
/// <para>
/// Circles live in the same database as Identity and Main — one deployable service, one connection
/// string (ADR-0003) — but in their <strong>own Postgres schema</strong> and their own migration
/// history. The seam stays visible, and splitting the context out later becomes a deployment change
/// rather than a rewrite (FEAT-003).
/// </para>
/// <para>
/// There is deliberately <strong>no foreign key to <c>AspNetUsers</c></strong>. A membership holds a
/// user identifier as a <em>value</em>, and an identifier that no longer resolves is a case the
/// context tolerates rather than a broken reference — the one-way rule ADR-0019 sets for
/// Secret-Santa members, applied here (arc42 §5.3).
/// </para>
/// <para>
/// The aggregate is mapped whole: memberships, invitations and decline records are children of the
/// circle with a required foreign key and a cascading delete, so deleting a circle takes everything
/// inside it in one transaction (FR-022) and nothing can be orphaned by a partial save.
/// </para>
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class CirclesDbContext(DbContextOptions<CirclesDbContext> options) : DbContext(options)
{
    /// <summary>The Postgres schema every table in this context lives in.</summary>
    public const string Schema = "circles";

    /// <summary>Gets the circles (ADR-0018).</summary>
    public DbSet<Circle> Circles => Set<Circle>();

    /// <summary>
    /// Gets the invitation attempts backing the three application-level invitation limits (FR-017).
    /// </summary>
    public DbSet<InvitationAttempt> InvitationAttempts => Set<InvitationAttempt>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Circle>(entity =>
        {
            entity.ToTable("circles");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);

            // User data, written once in the creator's language and never re-translated (FR-016).
            entity.Property(c => c.Label).IsRequired().HasMaxLength(50);
            entity.Property(c => c.CreatedAt).IsRequired();

            // Optimistic concurrency on the circle row. Postgres maintains xmin itself, so this
            // costs no column and no domain property — and because every write goes through
            // EfCircleRepository.SaveAsync, which touches the root row, a change anywhere inside
            // the aggregate bumps it. That is what stops two admins from both winning a transfer
            // (FR-020) rather than leaving the circle with two owners or none.
            //
            // Declared as a uint shadow property rather than through UseXminAsConcurrencyToken(),
            // which Npgsql 10 no longer ships: the provider convention recognises a uint concurrency
            // token generated on add-or-update and binds it to the system column, so no column is
            // created and no domain property is invented to carry a version.
            entity.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();

            // Nothing in M2 raises a domain event: the invitation mail is enqueued by the handler
            // through IInvitationMailer, and an event in between would be indirection with exactly
            // one consumer (FEAT-003).
            entity.Ignore(c => c.DomainEvents);

            // Computed from the memberships, and it throws when the invariant is broken — mapping
            // it would make EF materialize an aggregate by way of an exception.
            entity.Ignore(c => c.Owner);

            entity.HasMany(c => c.Memberships)
                .WithOne()
                .HasForeignKey("CircleId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Invitations)
                .WithOne()
                .HasForeignKey("CircleId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Declines)
                .WithOne()
                .HasForeignKey("CircleId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(c => c.Memberships)
                .HasField("_memberships")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Navigation(c => c.Invitations)
                .HasField("_invitations")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.Navigation(c => c.Declines)
                .HasField("_declines")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CircleMembership>(entity =>
        {
            entity.ToTable("circle_memberships");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedNever();

            // Null for a managed member — a person with a profile and no account (FR-019).
            entity.Property(m => m.UserId);
            entity.Property(m => m.DisplayName).IsRequired().HasMaxLength(100);

            // Culture name — "de-CH", "en", "fr-CH" (ADR-0011).
            entity.Property(m => m.Language).HasMaxLength(16);
            entity.Property(m => m.Role).IsRequired().HasConversion<string>().HasMaxLength(16);
            entity.Property(m => m.JoinedAt).IsRequired();

            // Derived from UserId being null.
            entity.Ignore(m => m.IsManaged);

            // "Which circles is this account in?" is the query behind every list and every
            // authorization check, and the account-deletion cleanup runs it too (FR-015).
            entity.HasIndex(m => m.UserId);

            // One account holds at most one membership per circle: acceptance checks it, and this
            // is what keeps two concurrent accepts from both creating one.
            entity.HasIndex("CircleId", nameof(CircleMembership.UserId))
                .IsUnique()
                .HasFilter(@"""UserId"" IS NOT NULL");
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).ValueGeneratedNever();

            // The only address M2 stores for someone who has not agreed to anything. It is kept
            // because sending and resending need it, and the row is deleted — never flagged — the
            // moment the invitation is consumed, revoked or expired (NFR-004).
            entity.Property(i => i.Email).IsRequired().HasMaxLength(320);
            entity.Property(i => i.EmailHash).IsRequired().HasMaxLength(64);
            entity.Property(i => i.InvitedByUserId).IsRequired();
            entity.Property(i => i.ManagedMembershipId);

            // The handle of the link's secret. The secret itself is never stored — the same rule as
            // refresh tokens, so a database dump cannot be replayed as an invitation link.
            entity.Property(i => i.SecretHandle).IsRequired().HasMaxLength(128);
            entity.Property(i => i.CreatedAt).IsRequired();
            entity.Property(i => i.ExpiresAt).IsRequired();

            // One live link at a time, across the whole context: the landing page resolves a link
            // by its handle without knowing which circle it belongs to.
            entity.HasIndex(i => i.SecretHandle).IsUnique();

            // "One pending invitation per address" (FR-017) is enforced by the aggregate; this is
            // the same rule at the level where two concurrent invites would otherwise both insert.
            entity.HasIndex("CircleId", nameof(Invitation.EmailHash)).IsUnique();

            // The join-request list finds the circles awaiting an address's answer, and the
            // retention purge sweeps by expiry.
            entity.HasIndex(i => i.EmailHash);
            entity.HasIndex(i => i.ExpiresAt);
        });

        modelBuilder.Entity<DeclineRecord>(entity =>
        {
            entity.ToTable("invitation_declines");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).ValueGeneratedNever();

            // The hash and nothing else: a circle keeps no address for someone who explicitly
            // declined to have anything to do with it (NFR-004).
            entity.Property(d => d.EmailHash).IsRequired().HasMaxLength(64);
            entity.Property(d => d.DeclinedAt).IsRequired();

            entity.HasIndex("CircleId", nameof(DeclineRecord.EmailHash)).IsUnique();
        });

        modelBuilder.Entity<InvitationAttempt>(entity =>
        {
            entity.ToTable("invitation_attempts");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedNever();
            entity.Property(a => a.InviterUserId).IsRequired();
            entity.Property(a => a.RecipientEmailHash).IsRequired().HasMaxLength(64);
            entity.Property(a => a.AttemptedAt).IsRequired();

            // Both counters are read as rolling windows over these two indexes.
            entity.HasIndex(a => new { a.InviterUserId, a.AttemptedAt });
            entity.HasIndex(a => new { a.RecipientEmailHash, a.AttemptedAt });
        });
    }
}
