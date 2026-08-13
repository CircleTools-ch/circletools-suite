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
}
