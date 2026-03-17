using Events.Abstractions;
using System.Threading.Channels;

namespace Events.Queue;

internal sealed class InMemoryTaskEventQueue
{
    private static Channel<IIntegrationEvent> CreateChannel(
        int capacity,
        bool singleReader,
        bool singleWriter) =>
        Channel.CreateBounded<IIntegrationEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = singleReader,
            SingleWriter = singleWriter
        });

    private readonly Channel<IIntegrationEvent> _incoming = CreateChannel(
        capacity: 50000,
        singleReader: true,
        singleWriter: false);

    private readonly Channel<IIntegrationEvent> _ready = CreateChannel(
        capacity: 50000,
        singleReader: true,
        singleWriter: false);

    private readonly Channel<IIntegrationEvent> _deadLetter = CreateChannel(
        capacity: 10000,
        singleReader: true,
        singleWriter: false);

    public ChannelReader<IIntegrationEvent> IncomingReader => _incoming.Reader;
    public ChannelWriter<IIntegrationEvent> IncomingWriter => _incoming.Writer;

    public ChannelReader<IIntegrationEvent> ReadyReader => _ready.Reader;
    public ChannelWriter<IIntegrationEvent> ReadyWriter => _ready.Writer;

    public ChannelReader<IIntegrationEvent> DeadLetterReader => _deadLetter.Reader;
    public ChannelWriter<IIntegrationEvent> DeadLetterWriter => _deadLetter.Writer;
}
