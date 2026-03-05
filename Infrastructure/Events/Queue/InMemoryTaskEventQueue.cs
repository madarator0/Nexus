using EventTaskManager.Application.Interface;
using System.Threading.Channels;

namespace Events.Queue;

internal sealed class InMemoryTaskEventQueue
{
    private readonly Channel<IIntegrationEvent> _incoming =
        Channel.CreateUnbounded<IIntegrationEvent>();

    private readonly Channel<IIntegrationEvent> _ready =
        Channel.CreateUnbounded<IIntegrationEvent>();

    public ChannelReader<IIntegrationEvent> IncomingReader => _incoming.Reader;

    public ChannelWriter<IIntegrationEvent> IncomingWriter => _incoming.Writer;

    public ChannelReader<IIntegrationEvent> ReadyReader => _ready.Reader;

    public ChannelWriter<IIntegrationEvent> ReadyWriter => _ready.Writer;
}