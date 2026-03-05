using Events.Queue;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Job;

internal sealed class IntegrationEventProcessorJob(
InMemoryTaskEventQueue queue,
IPublisher publisher,
ILogger<IntegrationEventProcessorJob> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(
            queue.ReadyReader.ReadAllAsync(stoppingToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 5,
                CancellationToken = stoppingToken
            },
            async (integrationEvent, ct) =>
            {
                try
                {
                    logger.LogInformation(
                        "Publishing {IntegrationEventId}",
                        integrationEvent.Id);

                    await publisher.Publish(integrationEvent, ct);

                    logger.LogInformation(
                        "Processed {IntegrationEventId}",
                        integrationEvent.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Error processing {IntegrationEventId}",
                        integrationEvent.Id);

                    var now = DateTime.UtcNow;

                    if (integrationEvent.RetryCount <= integrationEvent.MaxRetries)
                    {
                        integrationEvent.RetryCount++;
                        integrationEvent.ExecuteAfter = now.AddSeconds(10 * integrationEvent.RetryCount);
                        logger.LogInformation(
                            "Rescheduling {IntegrationEventId} for retry #{RetryCount} at {ExecuteAfter}",
                            integrationEvent.Id,
                            integrationEvent.RetryCount,
                            integrationEvent.ExecuteAfter);
                        await queue.IncomingWriter.WriteAsync(integrationEvent, ct);
                    }
                }
            });
    }
}