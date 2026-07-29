using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Email;
using Microsoft.AspNetCore.Mvc;

namespace BackoffBus.Sandbox.RabbitMQ.Api.Controllers;

/// <summary>Publishes email-delivery events to RabbitMQ.</summary>
[ApiController]
[Route("emails")]
public sealed class EmailsController : ControllerBase
{
    /// <summary>Publishes an email-delivery event to RabbitMQ.</summary>
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
            Provider = "RabbitMQ",
            Status = "Published"
        });
    }

    /// <summary>Describes an email to deliver.</summary>
    /// <param name="Recipient">The destination email address.</param>
    /// <param name="Subject">The email subject.</param>
    /// <param name="Body">The email body.</param>
    public sealed record SendEmailRequest(
        string Recipient,
        string Subject,
        string Body);
}
