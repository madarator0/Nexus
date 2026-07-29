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
    private readonly SemaphoreSlim _acknowledgementGate = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = checked((ushort)Math.Min(
            _options.ProcessorConcurrency,
            ushort.MaxValue));
        await using var channel =
            await transport.CreateConsumerChannelAsync(
                concurrency,
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

        try
        {
            var delay = envelope.ExecuteAfter - timeProvider.GetUtcNow();

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }

            logger.LogInformation(
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
        await _acknowledgementGate.WaitAsync(cancellationToken);

        try
        {
            await channel.BasicAckAsync(
                deliveryTag,
                multiple: false,
                cancellationToken);
        }
        finally
        {
            _acknowledgementGate.Release();
        }
    }

    private async ValueTask RejectAsync(
        IChannel channel,
        ulong deliveryTag,
        bool requeue,
        CancellationToken cancellationToken)
    {
        await _acknowledgementGate.WaitAsync(cancellationToken);

        try
        {
            await channel.BasicNackAsync(
                deliveryTag,
                multiple: false,
                requeue,
                cancellationToken);
        }
        finally
        {
            _acknowledgementGate.Release();
        }
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
