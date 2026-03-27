using Events.Queue;
using Events.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Events.Job;

internal sealed class IntegrationEventProcessorJob(
    InMemoryTaskEventQueue queue,
    IServiceProvider serviceProvider,
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

                    // ✅ создаём scope на каждое событие
                    using var scope = serviceProvider.CreateScope();

                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

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

                    await RetryAsync(integrationEvent, ct);
                }
            }
        );
    }

    public async Task RetryAsync(IIntegrationEvent integrationEvent, CancellationToken ct)
    {
        integrationEvent.RetryCount++;

        if (integrationEvent.RetryCount > integrationEvent.MaxRetries)
        {
            await queue.DeadLetterWriter.WriteAsync(integrationEvent, ct);
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Pow(2, integrationEvent.RetryCount));

        integrationEvent.ExecuteAfter = DateTime.UtcNow.Add(delay);

        await queue.IncomingWriter.WriteAsync(integrationEvent, ct);
    }
}