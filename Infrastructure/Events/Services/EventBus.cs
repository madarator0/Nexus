using Events.Queue;
using Events.Abstractions;

namespace Events.Services;

internal sealed class EventBus(InMemoryTaskEventQueue queue) : IEventBus
{
    public ValueTask PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class, IIntegrationEvent
    {
        var writer = integrationEvent.ExecuteAfter <= DateTime.UtcNow
            ? queue.ReadyWriter
            : queue.IncomingWriter;

        return writer.TryWrite(integrationEvent)
            ? ValueTask.CompletedTask
            : writer.WriteAsync(integrationEvent, cancellationToken);
    }
}
