using MediatR;
using Microsoft.Extensions.Logging;

namespace EventTaskManager.Application.TaskEvent.Test;

internal sealed class TestIntegrationEventHandler(ILogger<TestIntegrationEventHandler> logger) : INotificationHandler<TestIntegrationEvent>
{
    public Task Handle(TestIntegrationEvent notification, CancellationToken cancellationToken)
    {
        var random = new Random();

        if (random.Next(10) == 1)
        {
            throw new ArgumentException();
        } 

        logger.LogInformation(
            "TestIntegrationEvent handled at {Time}. EventId={Id}",
            DateTime.UtcNow,
            notification.Id);

        return Task.CompletedTask;
    }
}
