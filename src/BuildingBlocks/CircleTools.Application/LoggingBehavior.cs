// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace CircleTools.Application;

/// <summary>
/// Pipeline behavior that logs the start and completion of every request. Contains no
/// PII (only the request type name is logged), per ADR-0012.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="logger">The logger used to record request handling.</param>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(
        TRequest request, PipelineContinuation<TResponse> continuation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var requestName = typeof(TRequest).Name;
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Handling {RequestName}", requestName);
        }

        try
        {
            var response = await continuation();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Handled {RequestName}", requestName);
            }

            return response;
        }
        catch (Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(exception, "Error handling {RequestName}", requestName);
            }

            throw;
        }
    }
}
