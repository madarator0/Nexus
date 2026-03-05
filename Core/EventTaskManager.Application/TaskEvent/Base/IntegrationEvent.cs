using EventTaskManager.Application.Interface;

namespace EventTaskManager.Application.TaskEvent.Base;

public abstract record IntegrationEvent(Guid Id) : IIntegrationEvent
{
    public DateTime ExecuteAfter { get; set; } = DateTime.UtcNow;

    public int RetryCount { get; set; }

    public int MaxRetries { get; init; } = 5;
}
