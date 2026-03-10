using Events.Extensions;
using Events.Queue;
using EventTaskManager.Application.Interface;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

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

            if (!pq.TryPeek(out var next, out _))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            var delay = next.ExecuteAfter - now;

            if (delay <= TimeSpan.Zero)
            {
                pq.Dequeue();
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
                continue;
            }

            var hasData = await queue.IncomingReader
                .WaitToReadAsync(stoppingToken)
                .WaitAsync(delay);

            if (!hasData)
            {
                pq.Dequeue();
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