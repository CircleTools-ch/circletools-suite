// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// Purges outbox messages that are past their usefulness (NFR-004).
/// </summary>
/// <param name="context">The Main database context.</param>
/// <param name="options">Retention settings.</param>
/// <param name="clock">Time source, injected so the boundary is testable without waiting a week.</param>
/// <param name="logger">Diagnostics.</param>
/// <remarks>
/// <para>
/// A mail payload holds the recipient's address in plain text — for an invitation, the address of
/// someone who has agreed to nothing, and for the farewell mail, the address of an account that no
/// longer exists. Once a message is sent it has no further purpose, so it must not stay forever.
/// </para>
/// <para>
/// A message still pending after the same window is dropped too: whatever it carried (a verification
/// or reset link) is stale by then, and keeping it would only keep the address.
/// </para>
/// </remarks>
public sealed class OutboxPurge(
    MainDbContext context,
    IOptions<RetentionOptions> options,
    TimeProvider clock,
    ILogger<OutboxPurge> logger)
{
    /// <summary>Deletes every outbox message sent, or enqueued, before the retention window.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many messages were purged.</returns>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - options.Value.OutboxMessageLifetime;

        var purged = await context.OutboxMessages
            .Where(m => m.ProcessedAt < cutoff || (m.ProcessedAt == null && m.EnqueuedAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (purged > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Purged {Count} outbox messages older than {Cutoff:O}.", purged, cutoff);
        }

        return purged;
    }
}
