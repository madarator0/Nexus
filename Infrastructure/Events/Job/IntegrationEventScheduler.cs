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
                if (!await queue.IncomingReader.WaitToReadAsync(stoppingToken))
                    break;

                DrainIncoming(pq);
                continue;
            }
            
            DrainIncoming(pq);

            var next = pq.Peek();
            var now = DateTime.UtcNow;
            var delay = next.ExecuteAfter - now;

            if (delay <= TimeSpan.Zero)
            {
                pq.Dequeue();
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
                continue;
            }

            var readTask = queue.IncomingReader.WaitToReadAsync(stoppingToken).AsTask();

            try
            {
                await readTask.WaitAsync(delay, stoppingToken);

                DrainIncoming(pq);
            }
            catch (TimeoutException)
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