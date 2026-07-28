using BackoffBus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.Application.IntegrationEvents.Test;

public sealed class JsonRoundTripIntegrationEventHandler(
    ILogger<JsonRoundTripIntegrationEventHandler> logger)
    : IIntegrationEventHandler<JsonRoundTripIntegrationEvent>
{
    public ValueTask HandleAsync(
        JsonRoundTripIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "JsonRoundTripIntegrationEvent handled at {Time}. EventId={Id}. Message={Message}. Attempt={Attempt}. CreatedAtUtc={CreatedAtUtc}",
            DateTime.UtcNow,
            integrationEvent.Id,
            integrationEvent.Message,
            integrationEvent.Attempt,
            integrationEvent.CreatedAtUtc);

        return ValueTask.CompletedTask;
    }
}
