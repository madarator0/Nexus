using Events.Queue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Job;

internal class DeadLetterIntegrationEventProcessorJob
    (
        InMemoryTaskEventQueue queue,
        ILogger<DeadLetterIntegrationEventProcessorJob> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(
            queue.DeadLetterReader.ReadAllAsync(stoppingToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 2,
                CancellationToken = stoppingToken
            },
            async (deadLetterEvent, ct) =>
            {
                logger.LogInformation("Processing dead letter event {DeadLetterEventId}", deadLetterEvent.Id);
            }
        );
    }
}
