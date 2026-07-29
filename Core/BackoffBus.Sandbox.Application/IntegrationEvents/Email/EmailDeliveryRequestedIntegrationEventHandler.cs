using BackoffBus.Abstractions;
using Microsoft.Extensions.Logging;

namespace BackoffBus.Sandbox.Application.IntegrationEvents.Email;

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
            "Simulating email delivery. EventId={EventId}; Recipient={Recipient}; Subject={Subject}",
            integrationEvent.Id,
            integrationEvent.Recipient,
            integrationEvent.Subject);

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            cancellationToken);

        logger.LogInformation(
            "Email delivery simulated successfully. EventId={EventId}; Recipient={Recipient}",
            integrationEvent.Id,
            integrationEvent.Recipient);
    }
}
