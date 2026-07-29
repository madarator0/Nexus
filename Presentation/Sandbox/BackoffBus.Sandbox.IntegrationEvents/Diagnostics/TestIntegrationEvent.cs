using BackoffBus.Events;
using BackoffBus.Serialization;

namespace BackoffBus.Sandbox.IntegrationEvents.Diagnostics;

[IntegrationEvent("sandbox.test", 1)]
public sealed record TestIntegrationEvent(
    Guid Id,
    string Message) : IntegrationEvent(Id)
{
    public override int MaxRetries => 3;
}
