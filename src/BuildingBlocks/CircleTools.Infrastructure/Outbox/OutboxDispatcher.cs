// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Infrastructure.Mail;
using Microsoft.Extensions.Logging;

namespace CircleTools.Infrastructure.Outbox;

/// <summary>Options controlling how the outbox is drained.</summary>
public sealed class OutboxDispatcherOptions
{
    /// <summary>Configuration section these options bind to.</summary>
    public const string SectionName = "Outbox";

    /// <summary>Gets or sets how long to wait between passes.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets how many messages one pass handles.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Gets or sets how many times a message is retried before being left alone.</summary>
    public int MaxAttempts { get; set; } = 5;
}

/// <summary>
/// Dispatches queued outbox messages — the second half of the queued-not-awaited rule that
/// NFR-005 depends on.
/// </summary>
/// <param name="store">Pending-message access.</param>
/// <param name="renderer">Turns payloads into localized mail.</param>
/// <param name="sender">The mail transport.</param>
/// <param name="clock">Time source.</param>
/// <param name="logger">Diagnostics.</param>
/// <remarks>
/// Runs outside the request, so a slow or failing mail provider can never change how long an
/// address-accepting endpoint takes to respond.
/// </remarks>
public sealed class OutboxDispatcher(
    IOutboxDispatchStore store,
    IMailTemplateRenderer renderer,
    IMailSender sender,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger)
{
    /// <summary>Runs one dispatch pass.</summary>
    /// <param name="batchSize">Maximum messages to handle.</param>
    /// <param name="maxAttempts">Skip messages that already failed this many times.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many messages were dispatched successfully.</returns>
    public async Task<int> DispatchPendingAsync(
        int batchSize,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var pending = await store.LoadPendingAsync(batchSize, maxAttempts, cancellationToken).ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        var sent = 0;

        foreach (var message in pending)
        {
            var mail = renderer.Render(message.Type, message.Payload);

            if (mail is null)
            {
                // Nothing can ever send this — an unknown type or an unusable payload. Marking it
                // processed stops it being retried forever at the head of the queue.
                logger.LogWarning(
                    "Outbox message {MessageId} of type {MessageType} could not be rendered; discarding.",
                    message.Id,
                    message.Type);
                message.MarkProcessed(clock.GetUtcNow());
                continue;
            }

            try
            {
                await sender.SendAsync(mail, cancellationToken).ConfigureAwait(false);
                message.MarkProcessed(clock.GetUtcNow());
                sent++;
            }
#pragma warning disable CA1031 // One bad message must not stop the rest of the batch.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Failed to send outbox message {MessageId}.", message.Id);
                message.MarkFailed(ex.Message);
            }
        }

        await store.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return sent;
    }
}
