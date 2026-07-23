using BackoffBus.TaskEvent.Base;

namespace BackoffBus.Sandbox.Application.TaskEvent.Test;

public record TestIntegrationEvent(Guid Id, string Message) : IntegrationEvent(Id);
