using EventTaskManager.Application.Interface;
using System.Threading.Channels;

namespace Events.Queue;

internal sealed class InMemoryTaskEventQueue
{
    private readonly Channel<IIntegrationEvent> _channel = Channel.CreateUnbounded<IIntegrationEvent>();

    public ChannelReader<IIntegrationEvent> Reader => _channel.Reader;

    public ChannelWriter<IIntegrationEvent> Writer => _channel.Writer;
}
