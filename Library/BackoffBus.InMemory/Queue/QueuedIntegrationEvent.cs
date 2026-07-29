using BackoffBus.Abstractions;

namespace BackoffBus.Queue;

internal readonly record struct QueuedIntegrationEvent(
    IIntegrationEvent IntegrationEvent,
    DateTimeOffset ExecuteAfter,
    int RetryCount = 0)
{
    public static QueuedIntegrationEvent Create(
        IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrationEvent),
                integrationEvent.MaxRetries,
                "The maximum retry count cannot be negative.");
        }

        return new QueuedIntegrationEvent(
            integrationEvent,
            integrationEvent.ExecuteAfter);
    }
}
