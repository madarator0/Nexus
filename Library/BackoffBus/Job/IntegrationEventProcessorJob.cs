using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.Queue;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackoffBus.Job;

internal sealed class IntegrationEventProcessorJob(
    InMemoryIntegrationEventQueue queue,
    IServiceProvider serviceProvider,
    IOptions<BackoffBusOptions> options,
    TimeProvider timeProvider,
    ILogger<IntegrationEventProcessorJob> logger) : BackgroundService
{
    private readonly BackoffBusOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Parallel.ForEachAsync(
            queue.ReadyReader.ReadAllAsync(stoppingToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.ProcessorConcurrency,
                CancellationToken = stoppingToken
            },
            ProcessAsync);
    }

    private async ValueTask ProcessAsync(
        QueuedIntegrationEvent queuedEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Publishing {IntegrationEventId}; retry {RetryCount}",
                queuedEvent.IntegrationEvent.Id,
                queuedEvent.RetryCount);

            await using var scope = serviceProvider.CreateAsyncScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            await publisher.Publish(
                queuedEvent.IntegrationEvent,
                cancellationToken);

            logger.LogInformation(
                "Processed {IntegrationEventId}",
                queuedEvent.IntegrationEvent.Id);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error processing {IntegrationEventId}; retry {RetryCount}",
                queuedEvent.IntegrationEvent.Id,
                queuedEvent.RetryCount);

            try
            {
                await RetryAsync(
                    queuedEvent,
                    exception,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception retryException)
            {
                logger.LogCritical(
                    retryException,
                    "Unable to retry or dead-letter integration event {IntegrationEventId}",
                    queuedEvent.IntegrationEvent.Id);
            }
        }
    }

    private async ValueTask RetryAsync(
        QueuedIntegrationEvent queuedEvent,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (queuedEvent.RetryCount
            >= queuedEvent.IntegrationEvent.MaxRetries)
        {
            var deadLetterEvent = new DeadLetterIntegrationEvent(
                queuedEvent.IntegrationEvent,
                queuedEvent.RetryCount,
                exception,
                timeProvider.GetUtcNow());

            await queue.DeadLetterWriter.WriteAsync(
                deadLetterEvent,
                cancellationToken);
            return;
        }

        var retryCount = queuedEvent.RetryCount + 1;
        var retryEvent = queuedEvent with
        {
            RetryCount = retryCount,
            ExecuteAfter = timeProvider.GetUtcNow().Add(
                CalculateRetryDelay(retryCount))
        };

        await queue.IncomingWriter.WriteAsync(
            retryEvent,
            cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int retryCount)
    {
        var exponent = Math.Min(retryCount - 1, 30);
        var delayTicks = Math.Min(
            _options.InitialRetryDelay.Ticks * Math.Pow(2, exponent),
            _options.MaximumRetryDelay.Ticks);

        return TimeSpan.FromTicks((long)delayTicks);
    }
}
