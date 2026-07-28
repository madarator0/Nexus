using BackoffBus.Serialization;
using BackoffBus.Events;

namespace BackoffBus.Sandbox.Application.IntegrationEvents.Test;

[IntegrationEvent("sandbox.json-round-trip", 1)]
public record JsonRoundTripIntegrationEvent(
    Guid Id,
    string Message,
    int Attempt,
    DateTimeOffset CreatedAtUtc) : IntegrationEvent(Id)
{
    public override int MaxRetries => 5;
}
