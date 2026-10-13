using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.Application;

/// <summary>Placeholder response used for commands that do not return a value.</summary>
internal readonly record struct Unit;

/// <summary>
/// Default <see cref="ISender"/> implementation. Resolves the handler for a request from
/// the <see cref="IServiceProvider"/> and runs it through the ordered pipeline behaviors.
/// </summary>
/// <param name="provider">The service provider used to resolve handlers and behaviors.</param>
public sealed class Sender(IServiceProvider provider) : ISender
{
    /// <inheritdoc />
    public Task Send(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Dispatch<Unit>(command, typeof(ICommandHandler<>), hasResult: false, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return Dispatch<TResult>(command, typeof(ICommandHandler<,>), hasResult: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Dispatch<TResult>(query, typeof(IQueryHandler<,>), hasResult: true, cancellationToken);
    }

    private async Task<TResult> Dispatch<TResult>(
        object request, Type openHandlerType, bool hasResult, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var handlerType = hasResult
            ? openHandlerType.MakeGenericType(requestType, typeof(TResult))
            : openHandlerType.MakeGenericType(requestType);

        var handler = provider.GetRequiredService(handlerType);
        var handleMethod = handlerType.GetMethod("HandleAsync")!;

        // The behavior pipeline runs over a boxed (object) response so that IPipelineBehavior<,> is
        // only ever closed over reference types. iOS Release is full-AOT (ADR-0072), and Mono full-AOT
        // cannot create a generic instantiation over a value-type argument at runtime: resolving the
        // behaviors closed over a value-type TResult — Guid (create/add) or the internal Unit of a void
        // command (edit/rename/delete) — threw "InvalidOperationException: AotCannotCreateGenericValueType"
        // on iPad, while Android and the web worked. Closing them over `object` uses shared generics,
        // which full-AOT supports; the real TResult is boxed out of the handler and unboxed once at the
        // end. Reference-type results already worked, so this is behaviour-preserving everywhere. See ADR-0074.
        PipelineContinuation<object?> pipeline = async () =>
        {
            var invocation = handleMethod.Invoke(handler, [request, cancellationToken]);
            if (hasResult)
            {
                return await (Task<TResult>)invocation!;
            }

            await (Task)invocation!;
            return default(TResult);
        };

        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(object));
        var behaviors = ((IEnumerable<object?>)provider.GetServices(behaviorType)).ToArray();
        if (behaviors.Length > 0)
        {
            var behaviorMethod = behaviorType.GetMethod("HandleAsync")!;
            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i]!;
                var next = pipeline;
                pipeline = () => (Task<object?>)behaviorMethod.Invoke(behavior, [request, next, cancellationToken])!;
            }
        }

        return (TResult)(await pipeline())!;
    }
}
