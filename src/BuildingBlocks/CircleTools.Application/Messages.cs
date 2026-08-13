namespace CircleTools.Application;

/// <summary>Marker interface for a command that mutates state and returns no result.</summary>
public interface ICommand;

/// <summary>Marker interface for a command that mutates state and returns a result.</summary>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public interface ICommand<out TResult>;

/// <summary>Marker interface for a query that reads state and returns a result.</summary>
/// <typeparam name="TResult">The type of the result produced by the query.</typeparam>
public interface IQuery<out TResult>;

/// <summary>Handles a command that returns no result.</summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>Handles the command.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the command has been handled.</returns>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a command that returns a result.</summary>
/// <typeparam name="TCommand">The command type handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>Handles the command and returns its result.</summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the command result.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a query that returns a result.</summary>
/// <typeparam name="TQuery">The query type handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>Handles the query and returns its result.</summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the query result.</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>Represents the continuation of the behavior pipeline (the next behavior or the handler).</summary>
/// <typeparam name="TResponse">The response type of the pipeline.</typeparam>
/// <returns>A task producing the response.</returns>
public delegate Task<TResponse> PipelineContinuation<TResponse>();

/// <summary>
/// A cross-cutting behavior wrapping request handling (validation, logging,
/// transactions, domain-event dispatch, ...). Behaviors run in registration order.
/// </summary>
/// <typeparam name="TRequest">The request (command or query) type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>Runs this behavior and invokes <paramref name="continuation"/> to continue the pipeline.</summary>
    /// <param name="request">The request being handled.</param>
    /// <param name="continuation">The continuation that invokes the next behavior or the handler.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the response.</returns>
    Task<TResponse> HandleAsync(TRequest request, PipelineContinuation<TResponse> continuation, CancellationToken cancellationToken);
}

/// <summary>Dispatches commands and queries to their handlers through the behavior pipeline.</summary>
public interface ISender
{
    /// <summary>Sends a command that returns no result.</summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the command has been handled.</returns>
    Task Send(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>Sends a command that returns a result.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the command result.</returns>
    Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default);

    /// <summary>Sends a query that returns a result.</summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the query result.</returns>
    Task<TResult> Send<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default);
}
