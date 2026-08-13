using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CircleTools.Infrastructure.Outbox;

/// <summary>Drains the outbox on a timer for the lifetime of the host.</summary>
/// <param name="scopeFactory">Creates a scope per pass, so each gets a fresh DbContext.</param>
/// <param name="options">Poll interval, batch size and retry limit.</param>
/// <param name="logger">Diagnostics.</param>
public sealed class OutboxDispatcherService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxDispatcherOptions> options,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        using var timer = new PeriodicTimer(settings.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();

                await dispatcher
                    .DispatchPendingAsync(settings.BatchSize, settings.MaxAttempts, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // The loop must survive transient failures (database, SMTP).
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Outbox dispatch pass failed; retrying on the next tick.");
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
