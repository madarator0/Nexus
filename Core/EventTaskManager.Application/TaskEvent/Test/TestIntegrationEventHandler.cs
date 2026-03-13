using MediatR;
using Microsoft.Extensions.Logging;

namespace EventTaskManager.Application.TaskEvent.Test;

internal sealed class TestIntegrationEventHandler(ILogger<TestIntegrationEventHandler> logger) : INotificationHandler<TestIntegrationEvent>
{
    public async Task Handle(TestIntegrationEvent notification, CancellationToken cancellationToken)
    {
        await Task.Delay(5000);

        logger.LogInformation(
            "TestIntegrationEvent handled at {Time}. EventId={Id}. Message={Message}",
            DateTime.UtcNow,
            notification.Id,
            notification.Message);
    }
}
