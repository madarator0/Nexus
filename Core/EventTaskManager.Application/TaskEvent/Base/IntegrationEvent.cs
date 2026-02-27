using EventTaskManager.Application.Interface;

namespace EventTaskManager.Application.TaskEvent.Base;

public abstract record IntegrationEvent(Guid Id) : IIntegrationEvent;
