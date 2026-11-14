// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Service.Core.Groups;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CircleTools.SecretSanta.Service;

/// <summary>
/// Once a day, deletes the online groups no account owns that nobody has used for the retention window (NFR-010,
/// ADR-0086). Every use refreshes a group's last activity, so a group in use is never swept; a device that still holds
/// a swept group keeps its own copy. The same sweep deletes invitations that lapsed unanswered (NFR-004).
/// </summary>
/// <param name="scopeFactory">Creates a scope per sweep for the scoped group service.</param>
/// <param name="logger">The sweep logger.</param>
public sealed class GroupRetentionService(IServiceScopeFactory scopeFactory, ILogger<GroupRetentionService> logger) : BackgroundService
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var groups = scope.ServiceProvider.GetRequiredService<GroupService>();
                var removed = await groups.PruneInactiveAsync(stoppingToken).ConfigureAwait(false);
                if (removed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Retention: removed {Count} inactive online group(s) without an account owner.", removed);
                }

                var lapsed = await groups.PruneExpiredInvitationsAsync(stoppingToken).ConfigureAwait(false);
                if (lapsed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Retention: removed {Count} expired group invitation(s).", lapsed);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
#pragma warning disable CA1031 // A background sweep must survive a transient failure and try again next tick.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Online-group retention sweep failed; will retry next interval.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
