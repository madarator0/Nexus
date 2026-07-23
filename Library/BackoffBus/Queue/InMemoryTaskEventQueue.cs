using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace BackoffBus.Queue;

internal sealed class InMemoryTaskEventQueue
{
    private readonly Channel<QueuedIntegrationEvent> _incoming;
    private readonly Channel<QueuedIntegrationEvent> _ready;
    private readonly Channel<DeadLetterIntegrationEvent> _deadLetter;

    public InMemoryTaskEventQueue(IOptions<BackoffBusOptions> options)
    {
        var currentOptions = Validate(options);

        _incoming = CreateChannel<QueuedIntegrationEvent>(
            currentOptions.IncomingQueueCapacity,
            singleReader: true,
            singleWriter: false);
        _ready = CreateChannel<QueuedIntegrationEvent>(
            currentOptions.ReadyQueueCapacity,
            singleReader: true,
            singleWriter: false);
        _deadLetter = CreateChannel<DeadLetterIntegrationEvent>(
            currentOptions.DeadLetterQueueCapacity,
            singleReader: true,
            singleWriter: false);
    }

    private static Channel<T> CreateChannel<T>(
        int capacity,
        bool singleReader,
        bool singleWriter) =>
        Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = singleReader,
            SingleWriter = singleWriter
        });

    public ChannelReader<QueuedIntegrationEvent> IncomingReader => _incoming.Reader;
    public ChannelWriter<QueuedIntegrationEvent> IncomingWriter => _incoming.Writer;

    public ChannelReader<QueuedIntegrationEvent> ReadyReader => _ready.Reader;
    public ChannelWriter<QueuedIntegrationEvent> ReadyWriter => _ready.Writer;

    public ChannelReader<DeadLetterIntegrationEvent> DeadLetterReader => _deadLetter.Reader;
    public ChannelWriter<DeadLetterIntegrationEvent> DeadLetterWriter => _deadLetter.Writer;

    private static BackoffBusOptions Validate(IOptions<BackoffBusOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Value.Validate();
        return options.Value;
    }
}
