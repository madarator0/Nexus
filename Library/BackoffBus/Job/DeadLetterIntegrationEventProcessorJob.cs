using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackoffBus.Job;

internal sealed class DeadLetterIntegrationEventProcessorJob(
    InMemoryTaskEventQueue queue,
    IServiceProvider serviceProvider,
    IOptions<BackoffBusOptions> options,
    ILogger<DeadLetterIntegrationEventProcessorJob> logger)
    : BackgroundService
{
    private readonly BackoffBusOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(
            queue.DeadLetterReader.ReadAllAsync(stoppingToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    _options.DeadLetterProcessorConcurrency,
                CancellationToken = stoppingToken
            },
            ProcessAsync);
    }

    private async ValueTask ProcessAsync(
        DeadLetterIntegrationEvent deadLetterEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<IDeadLetterIntegrationEventHandler>();

            await handler.HandleAsync(
                deadLetterEvent,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                exception,
                "Dead-letter handler failed for integration event {IntegrationEventId}. The in-memory event cannot be redelivered.",
                deadLetterEvent.IntegrationEvent.Id);
        }
    }
}
