using BackoffBus.RabbitMQ.Configuration;
using BackoffBus.RabbitMQ.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace BackoffBus.RabbitMQ.Services;

internal sealed class RabbitMqTransport : IAsyncDisposable
{
    private readonly RabbitMqBackoffBusOptions _options;
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly SemaphoreSlim _publisherGate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _publisherChannel;

    public RabbitMqTransport(
        IOptions<RabbitMqBackoffBusOptions> options,
        IOptions<BackoffBus.Configuration.BackoffBusOptions> busOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        options.Value.Validate();
        busOptions.Value.Validate();
        _options = options.Value;
        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = checked((ushort)Math.Min(
                busOptions.Value.ProcessorConcurrency,
                ushort.MaxValue))
        };
    }

    internal string QueueName => _options.QueueName;

    internal string DeadLetterQueueName => $"{_options.QueueName}.dead-letter";

    internal ushort PrefetchCount => _options.PrefetchCount;

    internal ValueTask PublishIntegrationEventAsync(
        BackoffBus.Abstractions.IIntegrationEvent integrationEvent,
        int retryCount,
        DateTimeOffset executeAfter,
        CancellationToken cancellationToken) =>
        PublishAsync(
            QueueName,
            integrationEvent.Id.ToString("D"),
            RabbitMqMessageSerializer.Serialize(
                integrationEvent,
                retryCount,
                executeAfter),
            cancellationToken);

    internal ValueTask PublishDeadLetterAsync(
        BackoffBus.DeadLetter.DeadLetterIntegrationEvent deadLetterEvent,
        CancellationToken cancellationToken) =>
        PublishAsync(
            DeadLetterQueueName,
            deadLetterEvent.IntegrationEvent.Id.ToString("D"),
            RabbitMqMessageSerializer.SerializeDeadLetter(deadLetterEvent),
            cancellationToken);

    internal async ValueTask<IChannel> CreateConsumerChannelAsync(
        ushort concurrency,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: concurrency),
            cancellationToken);
        await DeclareTopologyAsync(channel, cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken);
        return channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisherChannel is not null)
        {
            await _publisherChannel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _publisherGate.Dispose();
        _connectionGate.Dispose();
    }

    private async ValueTask PublishAsync(
        string queueName,
        string messageId,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        await _publisherGate.WaitAsync(cancellationToken);

        try
        {
            var channel = await GetPublisherChannelAsync(
                cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                MessageId = messageId,
                Persistent = _options.PersistentMessages
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body,
                cancellationToken);
        }
        finally
        {
            _publisherGate.Release();
        }
    }

    private async ValueTask<IChannel> GetPublisherChannelAsync(
        CancellationToken cancellationToken)
    {
        if (_publisherChannel is { IsOpen: true })
        {
            return _publisherChannel;
        }

        if (_publisherChannel is not null)
        {
            await _publisherChannel.DisposeAsync();
        }

        var connection = await GetConnectionAsync(cancellationToken);
        _publisherChannel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await DeclareTopologyAsync(_publisherChannel, cancellationToken);
        return _publisherChannel;
    }

    private async ValueTask<IConnection> GetConnectionAsync(
        CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionGate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }

            _connection = await _connectionFactory.CreateConnectionAsync(
                "BackoffBus",
                cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async ValueTask DeclareTopologyAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            queue: DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}
