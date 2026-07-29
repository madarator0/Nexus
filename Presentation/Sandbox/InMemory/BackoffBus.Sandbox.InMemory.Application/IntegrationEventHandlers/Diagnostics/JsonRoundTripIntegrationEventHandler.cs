using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.InMemory.Application.IntegrationEventHandlers.Diagnostics;

public sealed class JsonRoundTripIntegrationEventHandler(
    ILogger<JsonRoundTripIntegrationEventHandler> logger)
    : IIntegrationEventHandler<JsonRoundTripIntegrationEvent>
{
    public ValueTask HandleAsync(
        JsonRoundTripIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[InMemory] JSON round-trip event handled at {Time}. EventId={Id}. Message={Message}. Attempt={Attempt}. CreatedAtUtc={CreatedAtUtc}",
            DateTimeOffset.UtcNow,
            integrationEvent.Id,
            integrationEvent.Message,
            integrationEvent.Attempt,
            integrationEvent.CreatedAtUtc);

        return ValueTask.CompletedTask;
    }
}
