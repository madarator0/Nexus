using BackoffBus.Abstractions;
using BackoffBus.Sandbox.IntegrationEvents.Diagnostics;
using BackoffBus.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace BackoffBus.Sandbox.InMemory.Api.Controllers;

/// <summary>Exposes in-memory scheduling and serialization examples.</summary>
[ApiController]
[Route("diagnostics")]
public sealed class DiagnosticsController : ControllerBase
{
    /// <summary>Schedules three events in a non-chronological order.</summary>
    [HttpPost("schedule")]
    public async Task<IActionResult> ScheduleAsync(
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var delays = new[] { 15, 5, 10 };

        foreach (var delayInSeconds in delays)
        {
            await eventBus.PublishAsync(
                new TestIntegrationEvent(
                    Guid.NewGuid(),
                    $"Execute after {delayInSeconds} seconds")
                {
                    ExecuteAfter = now.AddSeconds(delayInSeconds)
                },
                cancellationToken);
        }

        return Accepted(new
        {
            Provider = "InMemory",
            ScheduledAt = now,
            DelaysInSeconds = delays
        });
    }

    /// <summary>Verifies event serialization and publishes the result.</summary>
    [HttpPost("json-round-trip")]
    public async Task<IActionResult> JsonRoundTripAsync(
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var integrationEvent = new JsonRoundTripIntegrationEvent(
            Guid.NewGuid(),
            "JSON round-trip works",
            1,
            DateTimeOffset.UtcNow);
        var json =
            IntegrationEventJsonSerializer.Serialize(integrationEvent);
        var restoredEvent =
            IntegrationEventJsonSerializer.Deserialize<
                JsonRoundTripIntegrationEvent>(json);

        await eventBus.PublishAsync(
            restoredEvent,
            cancellationToken);

        return Ok(new
        {
            Provider = "InMemory",
            Json = json,
            RestoredEvent = restoredEvent,
            Published = true
        });
    }
}
