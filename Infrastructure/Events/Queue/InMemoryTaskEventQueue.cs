using Events.Abstractions;
using System.Threading.Channels;

namespace Events.Queue;

internal sealed class InMemoryTaskEventQueue
{
    private static Channel<IIntegrationEvent> CreateChannel(int capacity) =>
        Channel.CreateBounded<IIntegrationEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

    private readonly Channel<IIntegrationEvent> _incoming = CreateChannel(50000);

    private readonly Channel<IIntegrationEvent> _ready = CreateChannel(50000);

    private readonly Channel<IIntegrationEvent> _deadLetter = CreateChannel(10000);

    public ChannelReader<IIntegrationEvent> IncomingReader => _incoming.Reader;
    public ChannelWriter<IIntegrationEvent> IncomingWriter => _incoming.Writer;

    public ChannelReader<IIntegrationEvent> ReadyReader => _ready.Reader;
    public ChannelWriter<IIntegrationEvent> ReadyWriter => _ready.Writer;

    public ChannelReader<IIntegrationEvent> DeadLetterReader => _deadLetter.Reader;
    public ChannelWriter<IIntegrationEvent> DeadLetterWriter => _deadLetter.Writer;
}