// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Application;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// The "after every change" auto-sync trigger (ADR-0044): a CQRS pipeline behavior that asks
/// <see cref="AutoSyncService"/> for a background sync once a <em>mutating</em> request has been
/// handled. One open-generic registration covers every create / edit / draw / delete, so no page has
/// to remember to sync.
/// </summary>
/// <remarks>
/// Queries are skipped — a read must never trigger a sync. Whether the closed request type is a
/// mutation is decided once per type (commands implement <see cref="ICommand"/> or
/// <see cref="ICommand{TResult}"/>; queries implement <see cref="IQuery{TResult}"/>), so the hot path
/// is a single field read. The sync itself is fire-and-forget inside <see cref="AutoSyncService"/>, so
/// this adds nothing to the command's own latency, and it runs only <em>after</em> the handler
/// succeeded (a throwing command never schedules a sync of a change that did not happen).
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="autoSync">The background sync coordinator.</param>
public sealed class AutoSyncBehavior<TRequest, TResponse>(AutoSyncService autoSync)
    : IPipelineBehavior<TRequest, TResponse>
{
    // Commands mutate; queries read. Computed once per closed generic type, not per dispatch.
    private static readonly bool IsMutation =
        !Array.Exists(
            typeof(TRequest).GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>));

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request, PipelineContinuation<TResponse> continuation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var response = await continuation();

        if (IsMutation)
        {
            autoSync.RequestSync();
        }

        return response;
    }
}
