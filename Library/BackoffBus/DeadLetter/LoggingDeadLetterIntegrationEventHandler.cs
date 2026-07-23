using BackoffBus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BackoffBus.DeadLetter;

internal sealed class LoggingDeadLetterIntegrationEventHandler(
    ILogger<LoggingDeadLetterIntegrationEventHandler> logger)
    : IDeadLetterIntegrationEventHandler
{
    public ValueTask HandleAsync(
        DeadLetterIntegrationEvent deadLetterEvent,
        CancellationToken cancellationToken)
    {
        logger.LogCritical(
            deadLetterEvent.Exception,
            "Integration event {IntegrationEventId} ({IntegrationEventType}) moved to dead letter after {RetryCount} retries at {FailedAt}",
            deadLetterEvent.IntegrationEvent.Id,
            deadLetterEvent.IntegrationEvent.GetType().FullName,
            deadLetterEvent.RetryCount,
            deadLetterEvent.FailedAt);

        return ValueTask.CompletedTask;
    }
}
