using Events.Queue;
using EventTaskManager.Application.Interface;

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