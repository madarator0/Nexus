using BackoffBus.Configuration;
using BackoffBus.RabbitMQ.Configuration;
using System.Globalization;

namespace BackoffBus.RabbitMQ.Services;

internal sealed class RabbitMqDelayQueueTopology
{
    private const long DefaultSchedulingResolutionMilliseconds = 1_000;
    private readonly string _queueName;
    private readonly RabbitMqDelayBucketSelection _bucketSelection;

    public RabbitMqDelayQueueTopology(
        string queueName,
        BackoffBusOptions options,
        RabbitMqDelayBucketSelection bucketSelection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (!Enum.IsDefined(bucketSelection))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bucketSelection));
        }

        _queueName = queueName;
        _bucketSelection = bucketSelection;
        DelayQueues = CreateDelayQueues(queueName, options);
    }

    internal IReadOnlyList<RabbitMqDelayQueue> DelayQueues { get; }

    internal string GetDestinationQueue(
        DateTimeOffset executeAfter,
        DateTimeOffset utcNow)
    {
        var remaining = executeAfter - utcNow;

        if (remaining <= TimeSpan.Zero)
        {
            return _queueName;
        }

        var remainingMilliseconds = remaining.TotalMilliseconds;
        return _bucketSelection switch
        {
            RabbitMqDelayBucketSelection.Ceiling =>
                SelectCeilingQueue(remainingMilliseconds).Name,
            RabbitMqDelayBucketSelection.Floor =>
                SelectFloorQueue(remainingMilliseconds).Name,
            _ => throw new InvalidOperationException(
                $"Unsupported delay bucket selection '{_bucketSelection}'.")
        };
    }

    private RabbitMqDelayQueue SelectFloorQueue(
        double remainingMilliseconds)
    {
        var selectedQueue = DelayQueues[0];

        foreach (var delayQueue in DelayQueues)
        {
            if (delayQueue.MessageTtlMilliseconds
                > remainingMilliseconds)
            {
                break;
            }

            selectedQueue = delayQueue;
        }

        return selectedQueue;
    }

    private RabbitMqDelayQueue SelectCeilingQueue(
        double remainingMilliseconds)
    {
        foreach (var delayQueue in DelayQueues)
        {
            if (delayQueue.MessageTtlMilliseconds
                >= remainingMilliseconds)
            {
                return delayQueue;
            }
        }

        return DelayQueues[^1];
    }

    private static IReadOnlyList<RabbitMqDelayQueue> CreateDelayQueues(
        string queueName,
        BackoffBusOptions options)
    {
        var initialRetryDelay = ToMilliseconds(
            options.InitialRetryDelay);
        var maximumRetryDelay = ToMilliseconds(
            options.MaximumRetryDelay);
        var minimumDelay = Math.Min(
            DefaultSchedulingResolutionMilliseconds,
            initialRetryDelay);
        var delays = new SortedSet<long>();

        AddExponentialDelays(
            delays,
            minimumDelay,
            maximumRetryDelay);
        AddExponentialDelays(
            delays,
            initialRetryDelay,
            maximumRetryDelay);

        return delays
            .Select(delay => new RabbitMqDelayQueue(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{queueName}.delay.{delay}ms"),
                delay))
            .ToArray();
    }

    private static void AddExponentialDelays(
        ISet<long> delays,
        long initialDelay,
        long maximumDelay)
    {
        var delay = initialDelay;

        while (true)
        {
            delays.Add(delay);

            if (delay >= maximumDelay)
            {
                return;
            }

            delay = Math.Min(delay * 2, maximumDelay);
        }
    }

    private static long ToMilliseconds(TimeSpan value) =>
        Math.Max(1, checked((long)Math.Ceiling(value.TotalMilliseconds)));
}

internal sealed record RabbitMqDelayQueue(
    string Name,
    long MessageTtlMilliseconds);
