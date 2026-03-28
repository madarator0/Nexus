using Events.TaskEvent.Base;

namespace EventTaskManager.Application.TaskEvent.Test;

public record JsonRoundTripIntegrationEvent(
    Guid Id,
    string Message,
    int Attempt,
    DateTime CreatedAtUtc) : IntegrationEvent(Id);
