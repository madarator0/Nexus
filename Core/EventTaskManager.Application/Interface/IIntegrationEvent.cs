using MediatR;

namespace EventTaskManager.Application.Interface;

public interface IIntegrationEvent : INotification
{
    Guid Id { get; init; }
}
