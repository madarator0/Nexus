namespace Events.Abstractions;

public interface IEventBus
{
    ValueTask PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent;
}
