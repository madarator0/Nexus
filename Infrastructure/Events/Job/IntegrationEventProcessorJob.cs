using Events.Queue;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Job
{
    internal sealed class IntegrationEventProcessorJob(
        InMemoryTaskEventQueue queue,
        IPublisher publisher,
        ILogger<IntegrationEventProcessorJob> logger
    ) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Parallel.ForEachAsync(
                queue.Reader.ReadAllAsync(stoppingToken),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 2,
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
                    }
                });
        }
    }
}
