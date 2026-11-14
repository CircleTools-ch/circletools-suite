// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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

/// <summary>A test command that returns a <b>value-type</b> result (the shape that broke on iOS full-AOT, ADR-0074).</summary>
public sealed record NewIdCommand : ICommand<Guid>;

/// <summary>Handler for <see cref="NewIdCommand"/>; returns a fixed id so the result is assertable.</summary>
public sealed class NewIdCommandHandler : ICommandHandler<NewIdCommand, Guid>
{
    /// <summary>The id this handler always returns.</summary>
    public static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <inheritdoc />
    public Task<Guid> HandleAsync(NewIdCommand command, CancellationToken cancellationToken) => Task.FromResult(Id);
}

/// <summary>A test command that returns <b>no</b> result (dispatched over the internal <c>Unit</c> struct, ADR-0074).</summary>
public sealed record TouchCommand : ICommand;

/// <summary>Handler for <see cref="TouchCommand"/>; records that it ran so the void pipeline is observable.</summary>
public sealed class TouchCommandHandler : ICommandHandler<TouchCommand>
{
    /// <summary>Whether <see cref="HandleAsync"/> has run.</summary>
    public bool Handled { get; private set; }

    /// <inheritdoc />
    public Task HandleAsync(TouchCommand command, CancellationToken cancellationToken)
    {
        Handled = true;
        return Task.CompletedTask;
    }
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
