// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CircleTools.Application;
using CircleTools.Main.Service.Test.TestSupport;

namespace CircleTools.Main.Service.Test;

/// <summary>Tests for the hand-rolled CQRS dispatcher and its pipeline.</summary>
public sealed class DispatcherTests
{
    [Fact]
    public async Task Send_RunsHandler_ThroughLoggingBehavior()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddApplication();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingCommandHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(new PingCommand("hi"));

        Assert.Equal("pong:hi", result);
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Handling PingCommand", StringComparison.Ordinal));
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Handled PingCommand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Send_ValueTypeResult_RunsThroughPipelineAndReturnsResult()
    {
        // Regression for ADR-0074: a value-type result (Guid) must survive the boxed-object pipeline.
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddApplication();
        services.AddScoped<ICommandHandler<NewIdCommand, Guid>, NewIdCommandHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(new NewIdCommand());

        Assert.Equal(NewIdCommandHandler.Id, result);
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Handled NewIdCommand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Send_VoidCommand_RunsThroughPipeline()
    {
        // Regression for ADR-0074: a void command dispatches over the internal Unit struct (also a value type).
        var loggerProvider = new RecordingLoggerProvider();
        var handler = new TouchCommandHandler();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddApplication();
        services.AddScoped<ICommandHandler<TouchCommand>>(_ => handler);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new TouchCommand());

        Assert.True(handler.Handled);
        Assert.Contains(loggerProvider.Messages, message => message.Contains("Handled TouchCommand", StringComparison.Ordinal));
    }
}
