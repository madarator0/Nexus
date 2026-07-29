using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Email;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.InMemory.Application.IntegrationEventHandlers.Email;

public sealed class EmailDeliveryRequestedIntegrationEventHandler(
    ILogger<EmailDeliveryRequestedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<
        EmailDeliveryRequestedIntegrationEvent>
{
    public async ValueTask HandleAsync(
        EmailDeliveryRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[InMemory] Simulating email delivery. EventId={EventId}; Recipient={Recipient}; Subject={Subject}",
            integrationEvent.Id,
            integrationEvent.Recipient,
            integrationEvent.Subject);

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            cancellationToken);

        logger.LogInformation(
            "[InMemory] Email delivery simulated successfully. EventId={EventId}; Recipient={Recipient}",
            integrationEvent.Id,
            integrationEvent.Recipient);
    }
}
