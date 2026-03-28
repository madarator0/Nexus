using MediatR;
using Microsoft.Extensions.Logging;

namespace EventTaskManager.Application.TaskEvent.Test;

internal sealed class JsonRoundTripIntegrationEventHandler(
    ILogger<JsonRoundTripIntegrationEventHandler> logger) : INotificationHandler<JsonRoundTripIntegrationEvent>
{
    public Task Handle(
        JsonRoundTripIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "JsonRoundTripIntegrationEvent handled at {Time}. EventId={Id}. Message={Message}. Attempt={Attempt}. CreatedAtUtc={CreatedAtUtc}",
            DateTime.UtcNow,
            notification.Id,
            notification.Message,
            notification.Attempt,
            notification.CreatedAtUtc);

        return Task.CompletedTask;
    }
}
