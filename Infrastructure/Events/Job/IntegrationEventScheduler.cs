using Events.Extensions;
using Events.Queue;
using Events.Abstractions;
using Microsoft.Extensions.Hosting;

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
                if (!await queue.IncomingReader.WaitToReadAsync(stoppingToken))
                    break;
            }

            DrainIncoming(pq);
            await ReleaseDueEventsAsync(pq, stoppingToken);

            if (!pq.TryPeek(out var next, out _))
            {
                continue;
            }

            var delay = next.ExecuteAfter - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            var hasData = await queue.IncomingReader
                .WaitToReadAsync(stoppingToken)
                .WaitAsync(delay);

            if (!hasData)
            {
                await ReleaseDueEventsAsync(pq, stoppingToken);
            }
        }
    }

    private async ValueTask ReleaseDueEventsAsync(
        PriorityQueue<IIntegrationEvent, DateTime> pq,
        CancellationToken stoppingToken)
    {
        while (pq.TryPeek(out var next, out _) && next.ExecuteAfter <= DateTime.UtcNow)
        {
            pq.Dequeue();

            if (!queue.ReadyWriter.TryWrite(next))
            {
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
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
