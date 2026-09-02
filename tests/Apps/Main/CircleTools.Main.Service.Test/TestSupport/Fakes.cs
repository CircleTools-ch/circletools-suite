using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CircleTools.Application;

namespace CircleTools.Main.Service.Test.TestSupport;

/// <summary>A test command that returns a string result.</summary>
/// <param name="Text">Arbitrary payload echoed back by the handler.</param>
public sealed record PingCommand(string Text) : ICommand<string>;

/// <summary>Handler for <see cref="PingCommand"/>.</summary>
public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    /// <inheritdoc />
    public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult($"pong:{command.Text}");
}

/// <summary>Logger provider that records formatted log messages for assertions.</summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    /// <summary>Gets the recorded log messages.</summary>
    public ConcurrentQueue<string> Messages { get; } = new();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
    }
}
