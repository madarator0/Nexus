using BackoffBus.Abstractions;

namespace BackoffBus.RabbitMQ.Services;

internal sealed class RabbitMqEventBus(
    RabbitMqTransport transport) : IEventBus
{
    public ValueTask PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrationEvent),
                integrationEvent.MaxRetries,
                "The maximum retry count cannot be negative.");
        }

        return transport.PublishIntegrationEventAsync(
            integrationEvent,
            retryCount: 0,
            integrationEvent.ExecuteAfter,
            cancellationToken);
    }
}
