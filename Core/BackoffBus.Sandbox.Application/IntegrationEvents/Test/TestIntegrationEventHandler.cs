using BackoffBus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.Application.IntegrationEvents.Test;

public sealed class TestIntegrationEventHandler(
    ILogger<TestIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TestIntegrationEvent>
{
    public async ValueTask HandleAsync(
        TestIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await Task.Delay(5000, cancellationToken);

        logger.LogInformation(
            "TestIntegrationEvent handled at {Time}. EventId={Id}. Message={Message}",
            DateTime.UtcNow,
            integrationEvent.Id,
            integrationEvent.Message);
    }
}
