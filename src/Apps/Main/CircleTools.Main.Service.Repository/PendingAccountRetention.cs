using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository;

/// <summary>Retention settings for unverified accounts and sent mail (NFR-004).</summary>
public sealed class RetentionOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Retention";

    /// <summary>Gets or sets how long an unverified account is kept before purging.</summary>
    public TimeSpan PendingAccountLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Gets or sets how long an outbox message is kept after it was sent (or enqueued, if never sent).</summary>
    /// <remarks>The privacy policy promises this window; change both together.</remarks>
    public TimeSpan OutboxMessageLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Gets or sets how often the purge runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Purges accounts that were never verified (FR-007, NFR-004).
/// </summary>
/// <param name="context">The Main database context.</param>
/// <param name="options">Retention settings.</param>
/// <param name="clock">Time source, injected so the boundary is testable without waiting 30 days.</param>
/// <param name="logger">Diagnostics.</param>
/// <remarks>
/// A typo'd address would otherwise leave personal data sitting in the database forever attached to
/// an account nobody can reach.
/// </remarks>
public sealed class PendingAccountPurge(
    MainDbContext context,
    IOptions<RetentionOptions> options,
    TimeProvider clock,
    ILogger<PendingAccountPurge> logger)
{
    /// <summary>Deletes every unverified account past its retention window.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many accounts were purged.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - options.Value.PendingAccountLifetime;

        var expired = await context.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return 0;
        }

        // Verified accounts are never touched here, however old they are — retention applies to
        // accounts that were never proven, not to inactive ones.
        context.Users.RemoveRange(expired);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Purged {Count} unverified accounts created before {Cutoff:O}.",
                expired.Count,
                cutoff);
        }

        return expired.Count;
    }
}

/// <summary>Runs <see cref="PendingAccountPurge"/> and <see cref="OutboxPurge"/> on a timer.</summary>
/// <param name="scopeFactory">Creates a scope per pass.</param>
/// <param name="options">Retention settings.</param>
/// <param name="logger">Diagnostics.</param>
public sealed class MainRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<MainRetentionService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<PendingAccountPurge>()
                    .PurgeAsync(stoppingToken)
                    .ConfigureAwait(false);
                await scope.ServiceProvider
                    .GetRequiredService<OutboxPurge>()
                    .PurgeAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive transient database failures.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Retention purge failed; retrying on the next tick.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
