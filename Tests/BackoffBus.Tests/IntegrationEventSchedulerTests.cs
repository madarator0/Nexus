using BackoffBus.Configuration;
using BackoffBus.Events;
using BackoffBus.Job;
using BackoffBus.Queue;
using Microsoft.Extensions.Options;

namespace BackoffBus.Tests;

public sealed class IntegrationEventSchedulerTests
{
    [Fact]
    public async Task ScheduledEvent_IsReleasedWhenTimeProviderAdvances()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var start = new DateTimeOffset(
            2030,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(start);
        var queue = new InMemoryIntegrationEventQueue(
            Options.Create(new BackoffBusOptions()));
        var scheduler = new IntegrationEventScheduler(
            queue,
            timeProvider);
        var integrationEvent = new ScheduledIntegrationEvent(
            Guid.NewGuid())
        {
            ExecuteAfter = start.AddHours(1)
        };

        await scheduler.StartAsync(cancellationToken);

        try
        {
            await queue.IncomingWriter.WriteAsync(
                QueuedIntegrationEvent.Create(integrationEvent),
                cancellationToken);
            await timeProvider.TimerCreated.WaitAsync(
                TimeSpan.FromSeconds(1),
                cancellationToken);

            Assert.False(queue.ReadyReader.TryRead(out _));

            timeProvider.Advance(TimeSpan.FromHours(1));

            var queuedEvent = await queue.ReadyReader
                .ReadAsync(cancellationToken)
                .AsTask()
                .WaitAsync(
                    TimeSpan.FromSeconds(1),
                    cancellationToken);

            Assert.Same(
                integrationEvent,
                queuedEvent.IntegrationEvent);
        }
        finally
        {
            await scheduler.StopAsync(cancellationToken);
            scheduler.Dispose();
        }
    }

    private sealed record ScheduledIntegrationEvent(Guid EventId)
        : IntegrationEvent(EventId)
    {
        public override int MaxRetries => 0;
    }
}
