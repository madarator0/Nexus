using BackoffBus.Abstractions;
using BackoffBus.Events;
using BackoffBus.InMemory.Configuration;
using BackoffBus.Queue;
using BackoffBus.Services;
using Microsoft.Extensions.Options;

namespace BackoffBus.Tests;

public sealed class EventBusTests
{
    [Fact]
    public async Task PublishAsync_SnapshotsScheduleMetadata()
    {
        var queue = new InMemoryIntegrationEventQueue(
            Options.Create(new InMemoryBackoffBusOptions()));
        var eventBus = new EventBus(queue, TimeProvider.System);
        var originalSchedule = DateTimeOffset.UtcNow.AddHours(1);
        var integrationEvent = new MutableIntegrationEvent
        {
            ExecuteAfter = originalSchedule
        };

        await eventBus.PublishAsync(
            integrationEvent,
            TestContext.Current.CancellationToken);
        integrationEvent.ExecuteAfter = originalSchedule.AddHours(1);

        Assert.True(queue.IncomingReader.TryRead(out var queuedEvent));
        Assert.Equal(originalSchedule, queuedEvent.ExecuteAfter);
    }

    [Fact]
    public void IntegrationEventContract_DoesNotExposeScheduleSetter()
    {
        var executeAfter = typeof(IIntegrationEvent)
            .GetProperty(nameof(IIntegrationEvent.ExecuteAfter));

        Assert.NotNull(executeAfter);
        Assert.Null(executeAfter.SetMethod);
    }

    [Fact]
    public async Task PublishAsync_DefaultEvent_IsIndependentOfSystemClock()
    {
        var queue = new InMemoryIntegrationEventQueue(
            Options.Create(new InMemoryBackoffBusOptions()));
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(
                2000,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero));
        var eventBus = new EventBus(queue, timeProvider);
        var integrationEvent = new ImmediateIntegrationEvent(
            Guid.NewGuid());

        await eventBus.PublishAsync(
            integrationEvent,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            DateTimeOffset.MinValue,
            integrationEvent.ExecuteAfter);
        Assert.True(queue.ReadyReader.TryRead(out var queuedEvent));
        Assert.Same(
            integrationEvent,
            queuedEvent.IntegrationEvent);
    }

    private sealed class MutableIntegrationEvent : IIntegrationEvent
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset ExecuteAfter { get; set; }

        public int MaxRetries { get; } = 3;
    }

    private sealed record ImmediateIntegrationEvent(Guid EventId)
        : IntegrationEvent(EventId)
    {
        public override int MaxRetries => 0;
    }
}
