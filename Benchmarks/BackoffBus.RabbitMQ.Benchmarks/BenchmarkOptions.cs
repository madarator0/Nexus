using System.Globalization;
using BackoffBus.RabbitMQ.Configuration;

namespace BackoffBus.RabbitMQ.Benchmarks;

internal sealed record BenchmarkOptions(
    string ConnectionString,
    string QueueName,
    int Count,
    int WarmupCount,
    TimeSpan ScheduledDelay,
    int PublisherConcurrency,
    int PublisherChannelCount,
    int PublisherBatchSize,
    int PublisherBatchMinimumSize,
    TimeSpan PublisherBatchDelay,
    RabbitMqDelayBucketSelection DelayBucketSelection,
    int ConsumerConcurrency,
    ushort PrefetchCount,
    TimeSpan Timeout,
    bool ShowHelp)
{
    private static readonly HashSet<string> KnownArguments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "connection",
            "queue",
            "count",
            "warmup",
            "delay-ms",
            "publisher-concurrency",
            "publisher-channels",
            "publisher-batch-size",
            "publisher-batch-minimum-size",
            "publisher-batch-delay-ms",
            "delay-bucket",
            "consumer-concurrency",
            "prefetch",
            "timeout-seconds"
        };

    internal static BenchmarkOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];

            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Unknown argument '{argument}'.");
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException(
                    $"A value is required for '{argument}'.");
            }

            values[argument[2..]] = args[++index];
        }

        var unknownArgument = values.Keys.FirstOrDefault(
            key => !KnownArguments.Contains(key));

        if (unknownArgument is not null)
        {
            throw new ArgumentException(
                $"Unknown argument '--{unknownArgument}'.");
        }

        var count = GetInt(values, "count", 1_000, minimum: 1);
        var warmupCount = GetInt(
            values,
            "warmup",
            10,
            minimum: 0);
        var delayMilliseconds = GetInt(
            values,
            "delay-ms",
            5_000,
            minimum: 0);
        var publisherConcurrency = GetInt(
            values,
            "publisher-concurrency",
            8,
            minimum: 1);
        var publisherChannelCount = GetInt(
            values,
            "publisher-channels",
            4,
            minimum: 1);
        var publisherBatchSize = GetInt(
            values,
            "publisher-batch-size",
            100,
            minimum: 1);
        var publisherBatchMinimumSize = GetInt(
            values,
            "publisher-batch-minimum-size",
            Math.Min(32, publisherBatchSize),
            minimum: 1,
            maximum: publisherBatchSize);
        var publisherBatchDelayMilliseconds = GetInt(
            values,
            "publisher-batch-delay-ms",
            0,
            minimum: 0);
        var delayBucketSelection = GetDelayBucketSelection(values);
        var consumerConcurrency = GetInt(
            values,
            "consumer-concurrency",
            32,
            minimum: 1);
        var prefetch = GetInt(
            values,
            "prefetch",
            128,
            minimum: 1,
            maximum: ushort.MaxValue);
        var timeoutSeconds = GetInt(
            values,
            "timeout-seconds",
            120,
            minimum: 1);

        return new BenchmarkOptions(
            GetString(
                values,
                "connection",
                "amqp://guest:guest@localhost:5672/"),
            GetString(
                values,
                "queue",
                "backoff-bus.benchmark"),
            count,
            warmupCount,
            TimeSpan.FromMilliseconds(delayMilliseconds),
            publisherConcurrency,
            publisherChannelCount,
            publisherBatchSize,
            publisherBatchMinimumSize,
            TimeSpan.FromMilliseconds(
                publisherBatchDelayMilliseconds),
            delayBucketSelection,
            consumerConcurrency,
            checked((ushort)prefetch),
            TimeSpan.FromSeconds(timeoutSeconds),
            showHelp);
    }

    internal static void PrintUsage()
    {
        Console.WriteLine(
            """
            BackoffBus RabbitMQ delivery benchmark

            Options:
              --connection <uri>              AMQP URI
              --queue <name>                  Queue name
              --count <number>                Measured messages (default: 1000)
              --warmup <number>               Warm-up messages (default: 10)
              --delay-ms <number>             ExecuteAfter delay; 0 for throughput
              --publisher-concurrency <n>     Concurrent publishers (default: 8)
              --publisher-channels <n>        RabbitMQ publisher channels (default: 4)
              --publisher-batch-size <n>      Confirm batch size (default: 100)
              --publisher-batch-minimum-size <n>
                                                Minimum pipelined group (default: 32)
              --publisher-batch-delay-ms <n>  Maximum batch fill delay (default: 0)
              --delay-bucket <ceiling|floor>  Delay bucket strategy (default: ceiling)
              --consumer-concurrency <n>      Concurrent handlers (default: 32)
              --prefetch <number>              RabbitMQ prefetch (default: 128)
              --timeout-seconds <number>       Run timeout (default: 120)
              --help                          Show this help

            Accuracy:
              --delay-ms 5000

            Throughput:
              --delay-ms 0 --count 10000
            """);
    }

    private static string GetString(
        IReadOnlyDictionary<string, string> values,
        string name,
        string defaultValue)
    {
        var value = values.GetValueOrDefault(name, defaultValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }

    private static int GetInt(
        IReadOnlyDictionary<string, string> values,
        string name,
        int defaultValue,
        int minimum,
        int maximum = int.MaxValue)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return defaultValue;
        }

        if (!int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
            || value < minimum
            || value > maximum)
        {
            throw new ArgumentException(
                $"--{name} must be between {minimum} and {maximum}.");
        }

        return value;
    }

    private static RabbitMqDelayBucketSelection GetDelayBucketSelection(
        IReadOnlyDictionary<string, string> values)
    {
        var value = values.GetValueOrDefault(
            "delay-bucket",
            "ceiling");

        return value.ToLowerInvariant() switch
        {
            "ceiling" => RabbitMqDelayBucketSelection.Ceiling,
            "floor" => RabbitMqDelayBucketSelection.Floor,
            _ => throw new ArgumentException(
                "--delay-bucket must be either ceiling or floor.")
        };
    }
}
