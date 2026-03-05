using EventTaskManager.Application.Interface;
using Events.Queue;
using Microsoft.Extensions.Hosting;
using System.Threading;

namespace Events.Job;

internal sealed class IntegrationEventScheduler(
    InMemoryTaskEventQueue queue
) : BackgroundService
{
    private readonly SemaphoreSlim _signal = new(0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pq = new PriorityQueue<IIntegrationEvent, DateTime>();

        _ = Task.Run(async () =>
        {
            await foreach (var item in queue.IncomingReader.ReadAllAsync(stoppingToken))
            {
                pq.Enqueue(item, item.ExecuteAfter);

                _signal.Release(); // разбудить scheduler
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (pq.Count == 0)
            {
                await _signal.WaitAsync(stoppingToken);
                continue;
            }

            var next = pq.Peek();

            var delay = next.ExecuteAfter - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                var delayTask = Task.Delay(delay, stoppingToken);
                var signalTask = _signal.WaitAsync(stoppingToken);

                var completed = await Task.WhenAny(delayTask, signalTask);

                if (completed == signalTask)
                    continue;
            }

            next = pq.Dequeue();

            await queue.ReadyWriter.WriteAsync(next, stoppingToken);
        }
    }
}