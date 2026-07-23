using MediatR;

namespace BackoffBus.Abstractions;

public interface IIntegrationEvent : INotification
{
    Guid Id { get; init; }

    DateTime ExecuteAfter { get; set; }

    int RetryCount { get; set; }

    int MaxRetries { get; }
}
