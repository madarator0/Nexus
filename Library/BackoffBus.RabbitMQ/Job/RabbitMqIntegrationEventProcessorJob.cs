using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.RabbitMQ.Serialization;
using BackoffBus.RabbitMQ.Services;
using BackoffBus.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace BackoffBus.RabbitMQ.Job;

internal sealed class RabbitMqIntegrationEventProcessorJob(
    RabbitMqTransport transport,
    IServiceProvider serviceProvider,
    IOptions<BackoffBusOptions> options,
    TimeProvider timeProvider,
    ILogger<RabbitMqIntegrationEventProcessorJob> logger)
    : BackgroundService
{
    private readonly BackoffBusOptions _options = Validate(options);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Min(
            _options.ProcessorConcurrency,
            ushort.MaxValue);
        var prefetchPerChannel = checked((ushort)Math.Max(
            1,
            (transport.PrefetchCount + concurrency - 1)
            / concurrency));
        using var consumerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken);
        var consumers = Enumerable
            .Range(0, concurrency)
            .Select(_ => ConsumeAsync(
                prefetchPerChannel,
                consumerCancellation.Token))
            .ToArray();

        try
        {
            await await Task.WhenAny(consumers);
        }
        finally
        {
            await consumerCancellation.CancelAsync();

            try
            {
                await Task.WhenAll(consumers);
            }
            catch (OperationCanceledException)
                when (consumerCancellation.IsCancellationRequested)
            {
            }
        }
    }

    private async Task ConsumeAsync(
        ushort prefetchCount,
        CancellationToken stoppingToken)
    {
        await using var channel =
            await transport.CreateConsumerChannelAsync(
                prefetchCount,
                stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, eventArgs) =>
            ProcessDeliveryAsync(channel, eventArgs, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: transport.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task ProcessDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken stoppingToken)
    {
        RabbitMqMessageEnvelope envelope;
        IIntegrationEvent integrationEvent;

        try
        {
            envelope = RabbitMqMessageSerializer.Deserialize(
                eventArgs.Body.Span);
            integrationEvent =
                RabbitMqMessageSerializer.DeserializeIntegrationEvent(
                    envelope);
        }
        catch (Exception exception)
            when (exception is JsonException
                  or ArgumentException
                  or InvalidOperationException)
        {
            logger.LogCritical(
                exception,
                "Discarding invalid RabbitMQ message {MessageId}",
                eventArgs.BasicProperties.MessageId);
            await RejectAsync(
                channel,
                eventArgs.DeliveryTag,
                requeue: false,
                stoppingToken);
            return;
        }

        if (envelope.ExecuteAfter > timeProvider.GetUtcNow())
        {
            await RescheduleEarlyDeliveryAsync(
                channel,
                eventArgs.DeliveryTag,
                envelope,
                integrationEvent,
                stoppingToken);
            return;
        }

        try
        {
            logger.LogDebug(
                "Dispatching {IntegrationEventId}; retry {RetryCount}",
                integrationEvent.Id,
                envelope.RetryCount);

            await using var scope = serviceProvider.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider
                .GetRequiredService<IIntegrationEventDispatcher>();
            await dispatcher.DispatchAsync(
                integrationEvent,
                stoppingToken);

            await AcknowledgeAsync(
                channel,
                eventArgs.DeliveryTag,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            if (channel.IsOpen)
            {
                await RejectAsync(
                    channel,
                    eventArgs.DeliveryTag,
                    requeue: true,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error processing {IntegrationEventId}; retry {RetryCount}",
                integrationEvent.Id,
                envelope.RetryCount);
            await RetryOrDeadLetterAsync(
                channel,
                eventArgs.DeliveryTag,
                envelope,
                integrationEvent,
                exception,
                stoppingToken);
        }
    }

    private async Task RescheduleEarlyDeliveryAsync(
        IChannel channel,
        ulong deliveryTag,
        RabbitMqMessageEnvelope envelope,
        IIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug(
                "Rescheduling {IntegrationEventId} for {ExecuteAfter}",
                integrationEvent.Id,
                envelope.ExecuteAfter);
            await transport.PublishIntegrationEventAsync(
                integrationEvent,
                envelope.RetryCount,
                envelope.ExecuteAfter,
                cancellationToken);
            await AcknowledgeAsync(
                channel,
                deliveryTag,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (channel.IsOpen)
            {
                await RejectAsync(
                    channel,
                    deliveryTag,
                    requeue: true,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            logger.LogCritical(
                exception,
                "Unable to reschedule integration event {IntegrationEventId}",
                integrationEvent.Id);
            await RejectAsync(
                channel,
                deliveryTag,
                requeue: true,
                cancellationToken);
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        ulong deliveryTag,
        RabbitMqMessageEnvelope envelope,
        IIntegrationEvent integrationEvent,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            if (envelope.RetryCount >= integrationEvent.MaxRetries)
            {
                await transport.PublishDeadLetterAsync(
                    new DeadLetterIntegrationEvent(
                        integrationEvent,
                        envelope.RetryCount,
                        exception,
                        timeProvider.GetUtcNow()),
                    cancellationToken);
            }
            else
            {
                var retryCount = envelope.RetryCount + 1;
                await transport.PublishIntegrationEventAsync(
                    integrationEvent,
                    retryCount,
                    timeProvider.GetUtcNow().Add(
                        CalculateRetryDelay(retryCount)),
                    cancellationToken);
            }

            await AcknowledgeAsync(
                channel,
                deliveryTag,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            if (channel.IsOpen)
            {
                await RejectAsync(
                    channel,
                    deliveryTag,
                    requeue: true,
                    CancellationToken.None);
            }
        }
        catch (Exception publishException)
        {
            logger.LogCritical(
                publishException,
                "Unable to republish integration event {IntegrationEventId}",
                integrationEvent.Id);
            await RejectAsync(
                channel,
                deliveryTag,
                requeue: true,
                cancellationToken);
        }
    }

    private async ValueTask AcknowledgeAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        await channel.BasicAckAsync(
            deliveryTag,
            multiple: false,
            cancellationToken);
    }

    private async ValueTask RejectAsync(
        IChannel channel,
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await channel.BasicNackAsync(
            deliveryTag,
            multiple: false,
            requeue,
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

    private static BackoffBusOptions Validate(
        IOptions<BackoffBusOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Value.Validate();
        return options.Value;
    }
}
