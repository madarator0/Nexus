using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.Job;
using BackoffBus.Queue;
using BackoffBus.TaskEvent.Base;
using MediatR;
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
        var queue = new InMemoryTaskEventQueue(options);
        var services = new ServiceCollection()
            .AddSingleton<IPublisher, ThrowingPublisher>();
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

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task Publish(
            object notification,
            CancellationToken cancellationToken = default) =>
            Task.FromException(
                new InvalidOperationException("delivery failed"));

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.FromException(
                new InvalidOperationException("delivery failed"));
    }
}
