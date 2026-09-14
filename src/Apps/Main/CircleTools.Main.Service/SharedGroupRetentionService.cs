using CircleTools.Main.Service.Core.Domain;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service;

/// <summary>
/// Periodically prunes owner-less shared-by-link groups that have gone inactive past the retention
/// window (NFR-010, ADR-0058). Every read or write refreshes a group's last-activity time, so an
/// in-use group is never swept; a device whose group is swept falls back to on-this-device on its next
/// open (the local copy is never touched).
/// </summary>
/// <param name="scopeFactory">Creates a scope per sweep to resolve the scoped store.</param>
/// <param name="limits">The retention window (in days).</param>
/// <param name="clock">The application clock.</param>
/// <param name="logger">The sweep logger.</param>
public sealed class SharedGroupRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<SharedGroupLimits> limits,
    TimeProvider clock,
    ILogger<SharedGroupRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
#pragma warning disable CA1031 // A background sweep must survive a transient failure and try again next tick.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Shared-group retention sweep failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISharedGroupStore>();
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(limits.Value.RetentionDays);
        var removed = await store.PruneExpiredAsync(cutoff, cancellationToken).ConfigureAwait(false);
        if (removed > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Retention: removed {Count} inactive shared-by-link group(s).", removed);
        }
    }
}
