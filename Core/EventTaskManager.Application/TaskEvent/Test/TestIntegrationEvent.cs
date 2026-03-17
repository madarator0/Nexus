using Events.TaskEvent.Base;

namespace EventTaskManager.Application.TaskEvent.Test;

public record TestIntegrationEvent(Guid Id, string Message) : IntegrationEvent(Id);
