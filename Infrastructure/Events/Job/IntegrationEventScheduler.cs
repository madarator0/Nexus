using Events.Queue;
using EventTaskManager.Application.Interface;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace Events.Job;

internal sealed class IntegrationEventScheduler(
    InMemoryTaskEventQueue queue
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pq = new PriorityQueue<IIntegrationEvent, DateTime>();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (pq.Count == 0)
            {
                var item = await queue.IncomingReader.ReadAsync(stoppingToken);
                pq.Enqueue(item, item.ExecuteAfter);
                continue;
            }

            var next = pq.Peek();
            var delay = next.ExecuteAfter - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                pq.Dequeue();
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
                continue;
            }

            var delayTask = Task.Delay(delay, stoppingToken);
            var readTask = IncomingReadAsync(stoppingToken);

            var completed = await Task.WhenAny(delayTask, readTask);

            if (completed == readTask)
            {
                var item = await readTask;
                pq.Enqueue(item, item.ExecuteAfter);

                DrainIncoming(pq);
            }
            else
            {
                pq.Dequeue();
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
            }
        }
    }

    private Task<IIntegrationEvent> IncomingReadAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<IIntegrationEvent>(cancellationToken);
        }

        try
        {
            if (queue.IncomingReader.TryRead(out IIntegrationEvent? fastItem))
            {
                return Task.FromResult(fastItem);
            }
        }
        catch (Exception exc) when (exc is not (ChannelClosedException or OperationCanceledException))
        {
            return Task.FromException<IIntegrationEvent>(exc);
        }

        return ReadAsyncCore(cancellationToken);

        async Task<IIntegrationEvent> ReadAsyncCore(CancellationToken ct)
        {
            while (true)
            {
                if (!await queue.IncomingReader.WaitToReadAsync(ct).ConfigureAwait(false))
                {
                    throw new ChannelClosedException();
                }

                if (queue.IncomingReader.TryRead(out IIntegrationEvent? item))
                {
                    return item;
                }
            }
        }
    }

    private void DrainIncoming(PriorityQueue<IIntegrationEvent, DateTime> pq)
    {
        while (queue.IncomingReader.TryRead(out var item))
        {
            pq.Enqueue(item, item.ExecuteAfter);
        }
    }
}