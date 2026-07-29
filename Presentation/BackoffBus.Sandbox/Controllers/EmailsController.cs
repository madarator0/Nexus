using BackoffBus.Abstractions;
using BackoffBus.Sandbox.Application.IntegrationEvents.Email;
using Microsoft.AspNetCore.Mvc;

namespace BackoffBus.Sandbox.Controllers;

[ApiController]
[Route("emails")]
public sealed class EmailsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> SendAsync(
        SendEmailRequest request,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var integrationEvent =
            new EmailDeliveryRequestedIntegrationEvent(
                Guid.NewGuid(),
                request.Recipient,
                request.Subject,
                request.Body);

        await eventBus.PublishAsync(
            integrationEvent,
            cancellationToken);

        return Accepted(new
        {
            integrationEvent.Id,
            Status = "Queued"
        });
    }

    public sealed record SendEmailRequest(
        string Recipient,
        string Subject,
        string Body);
}
