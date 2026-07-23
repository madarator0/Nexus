using BackoffBus.TaskEvent.Base;

namespace BackoffBus.Sandbox.Application.TaskEvent.Test;

public record JsonRoundTripIntegrationEvent(
    Guid Id,
    string Message,
    int Attempt,
    DateTime CreatedAtUtc) : IntegrationEvent(Id);
