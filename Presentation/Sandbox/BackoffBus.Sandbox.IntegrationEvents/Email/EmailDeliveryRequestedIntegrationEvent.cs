using BackoffBus.Events;
using BackoffBus.Serialization;

namespace BackoffBus.Sandbox.IntegrationEvents.Email;

[IntegrationEvent("sandbox.email-delivery-requested", 1)]
public sealed record EmailDeliveryRequestedIntegrationEvent(
    Guid Id,
    string Recipient,
    string Subject,
    string Body) : IntegrationEvent(Id)
{
    public override int MaxRetries => 5;
}
