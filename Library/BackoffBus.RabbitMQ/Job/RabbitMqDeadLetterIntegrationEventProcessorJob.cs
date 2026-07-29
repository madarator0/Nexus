using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.RabbitMQ.Serialization;
using BackoffBus.RabbitMQ.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace BackoffBus.RabbitMQ.Job;

internal sealed class RabbitMqDeadLetterIntegrationEventProcessorJob(
    RabbitMqTransport transport,
    IServiceProvider serviceProvider,
    IOptions<BackoffBusOptions> options,
    ILogger<RabbitMqDeadLetterIntegrationEventProcessorJob> logger)
    : BackgroundService
{
    private readonly BackoffBusOptions _options = Validate(options);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Min(
            _options.DeadLetterProcessorConcurrency,
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
            queue: transport.DeadLetterQueueName,
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
        RabbitMqDeadLetterEnvelope envelope;
        BackoffBus.Abstractions.IIntegrationEvent integrationEvent;

        try
        {
            envelope = RabbitMqMessageSerializer.DeserializeDeadLetter(
                eventArgs.Body.Span);
            integrationEvent = RabbitMqMessageSerializer
                .DeserializeDeadLetterIntegrationEvent(envelope);
        }
        catch (Exception exception)
            when (exception is JsonException
                  or ArgumentException
                  or InvalidOperationException)
        {
            logger.LogCritical(
                exception,
                "Discarding invalid RabbitMQ dead-letter message {MessageId}",
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
            var exception = new RabbitMqDeliveryException(
                envelope.ExceptionType,
                envelope.ExceptionMessage,
                envelope.ExceptionStackTrace);
            var deadLetterEvent = new DeadLetterIntegrationEvent(
                integrationEvent,
                envelope.RetryCount,
                exception,
                envelope.FailedAt);

            await using var scope = serviceProvider.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<IDeadLetterIntegrationEventHandler>();
            await handler.HandleAsync(deadLetterEvent, stoppingToken);
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
            logger.LogCritical(
                exception,
                "Dead-letter handler failed for RabbitMQ message {MessageId}",
                eventArgs.BasicProperties.MessageId);
            await RejectAsync(
                channel,
                eventArgs.DeliveryTag,
                requeue: true,
                stoppingToken);
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

    private static BackoffBusOptions Validate(
        IOptions<BackoffBusOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Value.Validate();
        return options.Value;
    }
}
