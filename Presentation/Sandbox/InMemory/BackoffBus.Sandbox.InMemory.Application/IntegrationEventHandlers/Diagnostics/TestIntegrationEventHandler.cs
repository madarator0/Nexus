using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.InMemory.Application.IntegrationEventHandlers.Diagnostics;

public sealed class TestIntegrationEventHandler(
    ILogger<TestIntegrationEventHandler> logger)
    : IIntegrationEventHandler<TestIntegrationEvent>
{
    public async ValueTask HandleAsync(
        TestIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(5),
            cancellationToken);

        logger.LogInformation(
            "[InMemory] Test event handled at {Time}. EventId={Id}. Message={Message}",
            DateTimeOffset.UtcNow,
            integrationEvent.Id,
            integrationEvent.Message);
    }
}
