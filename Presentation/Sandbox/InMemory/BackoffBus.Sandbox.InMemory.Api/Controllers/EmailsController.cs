using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Email;
using Microsoft.AspNetCore.Mvc;

namespace BackoffBus.Sandbox.InMemory.Api.Controllers;

/// <summary>Publishes email-delivery events through the in-memory provider.</summary>
[ApiController]
[Route("emails")]
public sealed class EmailsController : ControllerBase
{
    /// <summary>Queues an email-delivery event in memory.</summary>
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
            Provider = "InMemory",
            Status = "Queued"
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
