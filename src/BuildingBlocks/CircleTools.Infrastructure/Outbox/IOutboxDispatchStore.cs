// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Infrastructure.Outbox;

/// <summary>Reads and updates pending outbox messages on behalf of the dispatcher.</summary>
public interface IOutboxDispatchStore
{
    /// <summary>Loads the oldest pending messages.</summary>
    /// <param name="batchSize">Maximum number to return.</param>
    /// <param name="maxAttempts">Skip messages that have already failed this many times.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending messages, oldest first.</returns>
    Task<IReadOnlyList<OutboxMessage>> LoadPendingAsync(
        int batchSize,
        int maxAttempts,
        CancellationToken cancellationToken = default);

    /// <summary>Persists the outcome of a dispatch pass.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once changes are written.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
