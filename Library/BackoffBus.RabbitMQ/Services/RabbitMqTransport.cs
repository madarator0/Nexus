using BackoffBus.RabbitMQ.Configuration;
using BackoffBus.RabbitMQ.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Threading.Channels;

namespace BackoffBus.RabbitMQ.Services;

internal sealed class RabbitMqTransport : IAsyncDisposable
{
    private readonly RabbitMqBackoffBusOptions _options;
    private readonly RabbitMqDelayQueueTopology _delayQueueTopology;
    private readonly TimeProvider _timeProvider;
    private readonly ConnectionFactory _connectionFactory;
    private readonly Channel<RabbitMqPublishCommand> _publishCommands;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly Task[] _publisherWorkers;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqTransport(
        IOptions<RabbitMqBackoffBusOptions> options,
        IOptions<BackoffBus.Configuration.BackoffBusOptions> busOptions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(busOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        options.Value.Validate();
        busOptions.Value.Validate();
        _options = options.Value;
        _delayQueueTopology = new RabbitMqDelayQueueTopology(
            _options.QueueName,
            busOptions.Value,
            _options.DelayBucketSelection);
        _timeProvider = timeProvider;
        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ConsumerDispatchConcurrency = checked((ushort)Math.Min(
                busOptions.Value.ProcessorConcurrency,
                ushort.MaxValue))
        };
        _publishCommands =
            Channel.CreateBounded<RabbitMqPublishCommand>(
                new BoundedChannelOptions(
                    _options.PublisherQueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader =
                        _options.PublisherChannelCount == 1,
                    SingleWriter = false
                });
        _publisherWorkers = Enumerable
            .Range(0, _options.PublisherChannelCount)
            .Select(_ => Task.Run(
                () => RunPublisherAsync(
                    _disposeCancellation.Token)))
            .ToArray();
    }

    internal string QueueName => _options.QueueName;

    internal string DeadLetterQueueName => $"{_options.QueueName}.dead-letter";

    internal ushort PrefetchCount => _options.PrefetchCount;

    internal ValueTask PublishIntegrationEventAsync(
        BackoffBus.Abstractions.IIntegrationEvent integrationEvent,
        int retryCount,
        DateTimeOffset executeAfter,
        CancellationToken cancellationToken)
    {
        var destinationQueue = _delayQueueTopology.GetDestinationQueue(
            executeAfter,
            _timeProvider.GetUtcNow());

        return PublishAsync(
            destinationQueue,
            integrationEvent.Id.ToString("D"),
            RabbitMqMessageSerializer.Serialize(
                integrationEvent,
                retryCount,
                executeAfter),
            cancellationToken);
    }

    internal ValueTask PublishDeadLetterAsync(
        BackoffBus.DeadLetter.DeadLetterIntegrationEvent deadLetterEvent,
        CancellationToken cancellationToken) =>
        PublishAsync(
            DeadLetterQueueName,
            deadLetterEvent.IntegrationEvent.Id.ToString("D"),
            RabbitMqMessageSerializer.SerializeDeadLetter(deadLetterEvent),
            cancellationToken);

    internal async ValueTask<IChannel> CreateConsumerChannelAsync(
        ushort prefetchCount,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: 1),
            cancellationToken);
        await DeclareTopologyAsync(channel, cancellationToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount,
            global: false,
            cancellationToken);
        return channel;
    }

    public async ValueTask DisposeAsync()
    {
        _publishCommands.Writer.TryComplete();
        await _disposeCancellation.CancelAsync();

        try
        {
            await Task.WhenAll(_publisherWorkers);
        }
        catch (Exception)
            when (_disposeCancellation.IsCancellationRequested)
        {
        }

        var disposedException = new ObjectDisposedException(
            nameof(RabbitMqTransport));

        while (_publishCommands.Reader.TryRead(out var command))
        {
            command.TrySetException(disposedException);
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _disposeCancellation.Dispose();
        _connectionGate.Dispose();
    }

    private async ValueTask PublishAsync(
        string queueName,
        string messageId,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var command = new RabbitMqPublishCommand(
            queueName,
            messageId,
            body,
            cancellationToken);

        try
        {
            await _publishCommands.Writer.WriteAsync(
                command,
                cancellationToken);
            await command.Completion;
        }
        catch (ChannelClosedException exception)
        {
            throw new ObjectDisposedException(
                nameof(RabbitMqTransport),
                exception);
        }
    }

    private async Task RunPublisherAsync(
        CancellationToken cancellationToken)
    {
        IChannel? channel = null;

        try
        {
            while (await _publishCommands.Reader.WaitToReadAsync(
                       cancellationToken))
            {
                if (!_publishCommands.Reader.TryRead(
                        out var firstCommand))
                {
                    continue;
                }

                var commands = await ReadPublishBatchAsync(
                    firstCommand,
                    cancellationToken);

                try
                {
                    if (channel is not { IsOpen: true })
                    {
                        if (channel is not null)
                        {
                            await channel.DisposeAsync();
                        }

                        channel = await CreatePublisherChannelAsync(
                            cancellationToken);
                    }

                    await PublishBatchAsync(channel, commands);
                }
                catch (Exception exception)
                {
                    foreach (var command in commands)
                    {
                        command.TrySetException(exception);
                    }
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }
        }
    }

    private async ValueTask<List<RabbitMqPublishCommand>>
        ReadPublishBatchAsync(
            RabbitMqPublishCommand firstCommand,
            CancellationToken cancellationToken)
    {
        var commands = new List<RabbitMqPublishCommand>(
            _options.PublisherBatchSize)
        {
            firstCommand
        };

        if (_options.PublisherBatchSize == 1)
        {
            return commands;
        }

        if (_options.PublisherBatchDelay <= TimeSpan.Zero
            && _publishCommands.Reader.CanCount
            && _publishCommands.Reader.Count + 1
            < _options.PublisherBatchMinimumSize)
        {
            return commands;
        }

        var startedAt = Stopwatch.GetTimestamp();

        while (commands.Count < _options.PublisherBatchSize)
        {
            while (commands.Count < _options.PublisherBatchSize
                   && _publishCommands.Reader.TryRead(out var command))
            {
                commands.Add(command);
            }

            if (commands.Count >= _options.PublisherBatchSize
                || _options.PublisherBatchDelay <= TimeSpan.Zero)
            {
                break;
            }

            var remaining = _options.PublisherBatchDelay
                            - Stopwatch.GetElapsedTime(startedAt);

            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            using var batchCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            batchCancellation.CancelAfter(remaining);

            try
            {
                if (!await _publishCommands.Reader.WaitToReadAsync(
                        batchCancellation.Token))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
                when (batchCancellation.IsCancellationRequested)
            {
                break;
            }
        }

        return commands;
    }

    private async ValueTask PublishBatchAsync(
        IChannel channel,
        IReadOnlyList<RabbitMqPublishCommand> commands)
    {
        if (commands.Count < _options.PublisherBatchMinimumSize)
        {
            foreach (var command in commands)
            {
                await PublishSingleAsync(channel, command);
            }

            return;
        }

        var confirmations = new Task[commands.Count];

        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];

            try
            {
                var properties = CreateBasicProperties(
                    command.MessageId);
                confirmations[index] = channel.BasicPublishAsync(
                        exchange: string.Empty,
                        routingKey: command.QueueName,
                        mandatory: true,
                        basicProperties: properties,
                        command.Body,
                        command.CancellationToken)
                    .AsTask();
            }
            catch (Exception exception)
            {
                confirmations[index] = Task.FromException(exception);
            }
        }

        try
        {
            await Task.WhenAll(confirmations);
        }
        catch
        {
        }

        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            var confirmation = confirmations[index];

            if (confirmation.IsCompletedSuccessfully)
            {
                command.TrySetResult();
            }
            else if (confirmation.IsCanceled)
            {
                command.TrySetCanceled();
            }
            else
            {
                command.TrySetException(
                    confirmation.Exception?.GetBaseException()
                    ?? new InvalidOperationException(
                        "RabbitMQ publisher confirmation failed."));
            }
        }
    }

    private async ValueTask PublishSingleAsync(
        IChannel channel,
        RabbitMqPublishCommand command)
    {
        try
        {
            var properties = CreateBasicProperties(command.MessageId);
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: command.QueueName,
                mandatory: true,
                basicProperties: properties,
                command.Body,
                command.CancellationToken);
            command.TrySetResult();
        }
        catch (OperationCanceledException)
            when (command.CancellationToken.IsCancellationRequested)
        {
            command.TrySetCanceled();
        }
        catch (Exception exception)
        {
            command.TrySetException(exception);
        }
    }

    private BasicProperties CreateBasicProperties(string messageId) =>
        new()
        {
            ContentType = "application/json",
            MessageId = messageId,
            Persistent = _options.PersistentMessages
        };

    private async ValueTask<IChannel> CreatePublisherChannelAsync(
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await DeclareTopologyAsync(channel, cancellationToken);
        return channel;
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

        foreach (var delayQueue in _delayQueueTopology.DelayQueues)
        {
            await channel.QueueDeclareAsync(
                queue: delayQueue.Name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] =
                        delayQueue.MessageTtlMilliseconds,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = QueueName
                },
                cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(
            queue: DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }
}

internal sealed class RabbitMqPublishCommand(
    string queueName,
    string messageId,
    ReadOnlyMemory<byte> body,
    CancellationToken cancellationToken)
{
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal string QueueName { get; } = queueName;

    internal string MessageId { get; } = messageId;

    internal ReadOnlyMemory<byte> Body { get; } = body;

    internal CancellationToken CancellationToken { get; } =
        cancellationToken;

    internal Task Completion => _completion.Task;

    internal void TrySetResult() => _completion.TrySetResult();

    internal void TrySetCanceled() =>
        _completion.TrySetCanceled(CancellationToken);

    internal void TrySetException(Exception exception) =>
        _completion.TrySetException(exception);
}
