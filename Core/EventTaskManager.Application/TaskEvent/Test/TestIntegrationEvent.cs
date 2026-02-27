using EventTaskManager.Application.TaskEvent.Base;

namespace EventTaskManager.Application.TaskEvent.Test;

public record TestIntegrationEvent(Guid Id) : IntegrationEvent(Id);
