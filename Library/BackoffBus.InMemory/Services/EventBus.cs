using BackoffBus.Abstractions;
using BackoffBus.Queue;

namespace BackoffBus.Services;

internal sealed class EventBus(
    InMemoryIntegrationEventQueue queue,
    TimeProvider timeProvider) : IEventBus
{
    public ValueTask PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class, IIntegrationEvent
    {
        var queuedEvent = QueuedIntegrationEvent.Create(integrationEvent);
        var writer = queuedEvent.ExecuteAfter <= timeProvider.GetUtcNow()
            ? queue.ReadyWriter
            : queue.IncomingWriter;

        return writer.TryWrite(queuedEvent)
            ? ValueTask.CompletedTask
            : writer.WriteAsync(queuedEvent, cancellationToken);
    }
}
