using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.Queue;
using BackoffBus.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace BackoffBus.Tests;

public sealed class EventBusTests
{
    [Fact]
    public async Task PublishAsync_SnapshotsScheduleMetadata()
    {
        var queue = new InMemoryTaskEventQueue(
            Options.Create(new BackoffBusOptions()));
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

    private sealed class MutableIntegrationEvent : IIntegrationEvent
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset ExecuteAfter { get; set; }

        public int MaxRetries { get; } = 3;
    }
}
