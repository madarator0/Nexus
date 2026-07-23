using BackoffBus.Serialization;
using BackoffBus.TaskEvent.Base;

namespace BackoffBus.Sandbox.Application.TaskEvent.Test;

[IntegrationEvent("sandbox.json-round-trip", 1)]
public record JsonRoundTripIntegrationEvent(
    Guid Id,
    string Message,
    int Attempt,
    DateTimeOffset CreatedAtUtc) : IntegrationEvent(Id)
{
    public override int MaxRetries => 5;
}
