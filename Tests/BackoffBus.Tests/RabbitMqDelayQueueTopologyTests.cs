using BackoffBus.Configuration;
using BackoffBus.RabbitMQ.Configuration;
using BackoffBus.RabbitMQ.Services;

namespace BackoffBus.Tests;

public sealed class RabbitMqDelayQueueTopologyTests
{
    [Fact]
    public void GetDestinationQueue_DueEvent_UsesMainQueue()
    {
        var topology = CreateTopology();
        var now = DateTimeOffset.UtcNow;

        var queue = topology.GetDestinationQueue(now, now);

        Assert.Equal("orders", queue);
    }

    [Fact]
    public void GetDestinationQueue_FutureEvent_UsesLargestDueBucket()
    {
        var topology = CreateTopology();
        var now = DateTimeOffset.UtcNow;

        var queue = topology.GetDestinationQueue(
            now.AddSeconds(7.5),
            now);

        Assert.Equal("orders.delay.4000ms", queue);
    }

    [Fact]
    public void GetDestinationQueue_Ceiling_UsesSmallestCoveringBucket()
    {
        var topology = CreateTopology(
            RabbitMqDelayBucketSelection.Ceiling);
        var now = DateTimeOffset.UtcNow;

        var queue = topology.GetDestinationQueue(
            now.AddSeconds(7.5),
            now);

        Assert.Equal("orders.delay.8000ms", queue);
    }

    [Fact]
    public void GetDestinationQueue_SubResolutionDelay_UsesSmallestBucket()
    {
        var topology = CreateTopology();
        var now = DateTimeOffset.UtcNow;

        var queue = topology.GetDestinationQueue(
            now.AddMilliseconds(250),
            now);

        Assert.Equal("orders.delay.1000ms", queue);
    }

    [Fact]
    public void DelayQueues_IncludeExactRetryBackoffBuckets()
    {
        var topology = new RabbitMqDelayQueueTopology(
            "orders",
            new BackoffBusOptions
            {
                InitialRetryDelay = TimeSpan.FromSeconds(3),
                MaximumRetryDelay = TimeSpan.FromSeconds(10)
            },
            RabbitMqDelayBucketSelection.Floor);

        var delays = topology.DelayQueues
            .Select(queue => queue.MessageTtlMilliseconds);

        Assert.Contains(3_000, delays);
        Assert.Contains(6_000, delays);
        Assert.Contains(10_000, delays);
    }

    private static RabbitMqDelayQueueTopology CreateTopology(
        RabbitMqDelayBucketSelection selection =
            RabbitMqDelayBucketSelection.Floor) =>
        new(
            "orders",
            new BackoffBusOptions
            {
                InitialRetryDelay = TimeSpan.FromSeconds(2),
                MaximumRetryDelay = TimeSpan.FromMinutes(5)
            },
            selection);
}
