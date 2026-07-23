using BackoffBus.Extensions;
using BackoffBus.Queue;
using Microsoft.Extensions.Hosting;

namespace BackoffBus.Job;

internal sealed class IntegrationEventScheduler(
    InMemoryTaskEventQueue queue,
    TimeProvider timeProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pq = new PriorityQueue<QueuedIntegrationEvent, DateTimeOffset>();

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

            var delay = next.ExecuteAfter - timeProvider.GetUtcNow();

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
        PriorityQueue<QueuedIntegrationEvent, DateTimeOffset> pq,
        CancellationToken stoppingToken)
    {
        while (pq.TryPeek(out var next, out _)
               && next.ExecuteAfter <= timeProvider.GetUtcNow())
        {
            pq.Dequeue();

            if (!queue.ReadyWriter.TryWrite(next))
            {
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
            }
        }
    }

    private void DrainIncoming(
        PriorityQueue<QueuedIntegrationEvent, DateTimeOffset> pq)
    {
        while (queue.IncomingReader.TryRead(out var item))
        {
            pq.Enqueue(item, item.ExecuteAfter);
        }
    }
}
