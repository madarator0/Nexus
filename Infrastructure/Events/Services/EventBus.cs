using Events.Queue;
using Events.Abstractions;

namespace Events.Services;

internal class EventBus(InMemoryTaskEventQueue queue) : IEventBus
{
    public async Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken)
        where T : class, IIntegrationEvent
    {
        await queue.IncomingWriter.WriteAsync(integrationEvent, cancellationToken);
    }
}