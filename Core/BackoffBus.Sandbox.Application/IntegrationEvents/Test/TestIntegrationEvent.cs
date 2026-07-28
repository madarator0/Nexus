using BackoffBus.Serialization;
using BackoffBus.Events;

namespace BackoffBus.Sandbox.Application.IntegrationEvents.Test;

[IntegrationEvent("sandbox.test", 1)]
public record TestIntegrationEvent(Guid Id, string Message) : IntegrationEvent(Id) 
{
    public override int MaxRetries => 3;
}

