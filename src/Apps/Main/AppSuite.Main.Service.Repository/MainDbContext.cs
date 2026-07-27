using AppSuite.Identity;
using AppSuite.Identity.Tokens;
using AppSuite.Main.Service.Core.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppSuite.Main.Service.Repository;

/// <summary>
/// EF Core database context for the Main module (PostgreSQL adapter).
/// </summary>
/// <remarks>
/// Also hosts the suite-wide Identity schema. Identity lives in the same database and the same
/// migration history as the rest of Main because there is one deployable service (ADR-0003);
/// the <em>bounded context</em> boundary is kept in code — the domain never references
/// <see cref="AppSuiteUser"/> — rather than by splitting the store (FEAT-002, arc42 §5.3).
/// </remarks>
/// <param name="options">The context options.</param>
public sealed class MainDbContext(DbContextOptions<MainDbContext> options)
    : IdentityDbContext<AppSuiteUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>Gets the heartbeats table.</summary>
    public DbSet<Heartbeat> Heartbeats => Set<Heartbeat>();

    /// <summary>Gets the refresh-token chains — one row per sign-in session (FR-008).</summary>
    public DbSet<RefreshTokenChain> RefreshTokenChains => Set<RefreshTokenChain>();

    /* Passkey storage (FR-010) is gated behind the Identity schema version, and the switch is
       IdentityOptions.Stores.SchemaVersion — configured in AddAppSuiteIdentityCore, NOT the
       protected SchemaVersion property on this context. Overriding that property reads back as
       3.0 and still leaves IdentityUserPasskey unmapped, which is a silent failure: passkey
       registration compiles and then hits a missing table at runtime.

       This is why MainDbContextFactory builds the context through a service provider rather
       than newing it up — a design-time factory without Identity configured generates a
       migration whose model differs from the one the app actually runs. */

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);

        builder.Entity<Heartbeat>(entity =>
        {
            entity.ToTable("heartbeats");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).ValueGeneratedNever();
            entity.Property(h => h.Note).IsRequired().HasMaxLength(500);
            entity.Property(h => h.CreatedAt).IsRequired();
            entity.Ignore(h => h.DomainEvents);
        });

        builder.Entity<AppSuiteUser>(entity =>
        {
            entity.Property(u => u.DisplayName).IsRequired().HasMaxLength(100);

            // Culture name — "de-CH", "en", "fr-CH" (ADR-0011).
            entity.Property(u => u.PreferredLanguage).IsRequired().HasMaxLength(16);
            entity.Property(u => u.CreatedAt).IsRequired();

            // Deliberately NOT unique: two "Anna"s in one family is ordinary, and
            // disambiguation is a UI concern rather than a domain constraint (FR-012).
            entity.HasIndex(u => u.DisplayName).IsUnique(false);

            // The retention job purges unverified accounts older than 30 days (NFR-004);
            // it filters on exactly this pair.
            entity.HasIndex(u => new { u.EmailConfirmed, u.CreatedAt });
        });

        builder.Entity<RefreshTokenChain>(entity =>
        {
            entity.ToTable("refresh_token_chains");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.Property(c => c.UserId).IsRequired();
            entity.Property(c => c.RevokedAt);

            // Computed from state — never stored.
            entity.Ignore(c => c.IsRevoked);
            entity.Ignore(c => c.Current);

            entity.HasIndex(c => c.UserId);

            entity.HasMany(c => c.Tokens)
                .WithOne()
                .HasForeignKey("ChainId")
                .IsRequired() // a refresh token without a chain has no revocation story
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(c => c.Tokens)
                .HasField("_tokens")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");

            // The handle is a hash of the secret handed to the client — never the secret.
            entity.HasKey(t => t.Handle);
            entity.Property(t => t.Handle).HasMaxLength(128);
            entity.Property(t => t.IssuedAt).IsRequired();
            entity.Property(t => t.ExpiresAt).IsRequired();
            entity.Property(t => t.ConsumedAt);
            entity.Property(t => t.SuccessorHandle).HasMaxLength(128);

            // Held only for the replay grace window, then nulled — see RefreshToken.
            entity.Property(t => t.SuccessorSecret).HasMaxLength(128);

            entity.Ignore(t => t.IsConsumed);
        });
    }
}
