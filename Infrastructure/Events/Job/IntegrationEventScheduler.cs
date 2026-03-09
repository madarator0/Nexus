using Events.Queue;
using EventTaskManager.Application.Interface;
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

                DrainIncoming(pq);
                continue;
            }

            DrainIncoming(pq);

            var next = pq.Peek();
            var delay = next.ExecuteAfter - DateTime.UtcNow;

            if (delay <= TimeSpan.Zero)
            {
                pq.Dequeue();
                await queue.ReadyWriter.WriteAsync(next, stoppingToken);
                continue;
            }

            var readTask = queue.IncomingReader.WaitToReadAsync(stoppingToken).AsTask();
            var delayTask = Task.Delay(delay, stoppingToken);

            var completed = await Task.WhenAny(readTask, delayTask);

            if (completed == readTask && await readTask)
            {
                DrainIncoming(pq);
            }
            else
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