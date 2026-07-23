using BackoffBus.Serialization;
using BackoffBus.TaskEvent.Base;

namespace BackoffBus.Sandbox.Application.TaskEvent.Test;

[IntegrationEvent("sandbox.test", 1)]
public record TestIntegrationEvent(Guid Id, string Message) : IntegrationEvent(Id) 
{
    public override int MaxRetries => 3;
}

