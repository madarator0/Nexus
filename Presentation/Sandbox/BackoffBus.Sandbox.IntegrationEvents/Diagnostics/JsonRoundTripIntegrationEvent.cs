using BackoffBus.Events;
using BackoffBus.Serialization;

namespace BackoffBus.Sandbox.IntegrationEvents.Diagnostics;

[IntegrationEvent("sandbox.json-round-trip", 1)]
public sealed record JsonRoundTripIntegrationEvent(
    Guid Id,
    string Message,
    int Attempt,
    DateTimeOffset CreatedAtUtc) : IntegrationEvent(Id)
{
    public override int MaxRetries => 5;
}
