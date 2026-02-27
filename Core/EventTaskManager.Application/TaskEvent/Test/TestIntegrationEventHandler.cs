using MediatR;

namespace EventTaskManager.Application.TaskEvent.Test;

internal sealed class TestIntegrationEventHandler : INotificationHandler<TestIntegrationEvent>
{
    public Task Handle(TestIntegrationEvent notification, CancellationToken cancellationToken)
    {
        return Task.Delay(5000, cancellationToken);
    }
}
