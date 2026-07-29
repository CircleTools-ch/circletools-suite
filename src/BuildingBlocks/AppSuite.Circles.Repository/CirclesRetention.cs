using AppSuite.Circles.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppSuite.Circles.Repository;

/// <summary>Retention settings for the circles schema (NFR-004).</summary>
public sealed class CirclesRetentionOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Circles:Retention";

    /// <summary>Gets or sets how often the purge runs.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Sweeps the circles schema of data that has outlived its purpose (NFR-004).
/// </summary>
/// <param name="context">The circles database context.</param>
/// <param name="limits">The configured lifetimes.</param>
/// <param name="clock">Time source, injected so the boundaries are testable without waiting.</param>
/// <param name="logger">Diagnostics.</param>
/// <remarks>
/// <para>
/// Three sweeps, and each one deletes something the product has no further business holding:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Expired invitations.</strong> A pending invitation is the only place M2 stores an address
/// belonging to someone who has not agreed to anything, so it must not outlive the fourteen days it
/// was acceptable for. The aggregate also drops them on the read paths; this is what removes the ones
/// in circles nobody opens.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Elapsed decline records.</strong> They hold a hash and a timestamp, and after the cooldown
/// they suppress nothing.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Counted invitation attempts</strong> older than the longest rate-limit window, which are
/// by then evidence for no decision anyone will make.
/// </description>
/// </item>
/// </list>
/// <para>
/// All three are leaf rows, so they are deleted in the database rather than loaded, tracked and
/// deleted — the sweep must stay cheap enough to run hourly on a table nobody is watching.
/// </para>
/// </remarks>
public sealed class CirclesPurge(
    CirclesDbContext context,
    IOptions<CircleLimits> limits,
    TimeProvider clock,
    ILogger<CirclesPurge> logger)
{
    /// <summary>Deletes everything past its retention boundary.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many rows were deleted, across all three sweeps.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var options = limits.Value;

        var invitations = await context.Set<Invitation>()
            .Where(i => i.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var declineCutoff = now - options.DeclineCooldown;
        var declines = await context.Set<DeclineRecord>()
            .Where(d => d.DeclinedAt <= declineCutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // The longest window any limit counts over. Nothing older can change a decision.
        var attemptCutoff = now - TimeSpan.FromDays(1);
        var attempts = await context.InvitationAttempts
            .Where(a => a.AttemptedAt <= attemptCutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = invitations + declines + attempts;

        if (total > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Purged {Invitations} expired invitations, {Declines} elapsed declines and "
                + "{Attempts} counted invitation attempts.",
                invitations,
                declines,
                attempts);
        }

        return total;
    }
}

/// <summary>Runs <see cref="CirclesPurge"/> on a timer.</summary>
/// <param name="scopeFactory">Creates a scope per pass.</param>
/// <param name="options">Retention settings.</param>
/// <param name="logger">Diagnostics.</param>
public sealed class CirclesRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<CirclesRetentionOptions> options,
    ILogger<CirclesRetentionService> logger) : BackgroundService
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
                    .GetRequiredService<CirclesPurge>()
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
                logger.LogError(ex, "Circles retention purge failed; retrying on the next tick.");
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
