using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.Job;
using BackoffBus.Queue;
using BackoffBus.Events;
using BackoffBus.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BackoffBus.Tests;

public sealed class IntegrationEventProcessorJobTests
{
    [Fact]
    public async Task FailedEvent_MovesToDeadLetterAfterRetryLimit()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;
        var options = Options.Create(new BackoffBusOptions());
        var queue = new InMemoryIntegrationEventQueue(options);
        var services = new ServiceCollection()
            .AddSingleton<
                IIntegrationEventDispatcher,
                ThrowingDispatcher>();
        await using var serviceProvider =
            services.BuildServiceProvider();
        var processor = new IntegrationEventProcessorJob(
            queue,
            serviceProvider,
            options,
            TimeProvider.System,
            NullLogger<IntegrationEventProcessorJob>.Instance);
        var integrationEvent = new FailingIntegrationEvent(
            Guid.NewGuid());

        await processor.StartAsync(cancellationToken);

        try
        {
            await queue.ReadyWriter.WriteAsync(
                QueuedIntegrationEvent.Create(integrationEvent),
                cancellationToken);
            var deadLetterEvent = await queue.DeadLetterReader.ReadAsync(
                cancellationToken);

            Assert.Same(
                integrationEvent,
                deadLetterEvent.IntegrationEvent);
            Assert.Equal(0, deadLetterEvent.RetryCount);
            Assert.Equal(
                "delivery failed",
                deadLetterEvent.Exception.Message);
        }
        finally
        {
            await processor.StopAsync(cancellationToken);
            processor.Dispose();
        }
    }

    private sealed record FailingIntegrationEvent(Guid EventId)
        : IntegrationEvent(EventId)
    {
        public override int MaxRetries => 0;
    }

    private sealed class ThrowingDispatcher
        : IIntegrationEventDispatcher
    {
        public ValueTask DispatchAsync(
            IIntegrationEvent integrationEvent,
            CancellationToken cancellationToken) =>
            new(Task.FromException(
                new InvalidOperationException("delivery failed")));
    }
}
