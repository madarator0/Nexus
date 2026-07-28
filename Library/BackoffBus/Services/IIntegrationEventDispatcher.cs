using BackoffBus.Abstractions;

namespace BackoffBus.Services;

internal interface IIntegrationEventDispatcher
{
    ValueTask DispatchAsync(
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
