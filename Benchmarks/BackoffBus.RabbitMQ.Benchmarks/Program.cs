using BackoffBus.Abstractions;
using BackoffBus.Extensions;
using BackoffBus.RabbitMQ.Benchmarks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;

BenchmarkOptions benchmarkOptions;

try
{
    benchmarkOptions = BenchmarkOptions.Parse(args);
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    BenchmarkOptions.PrintUsage();
    return 1;
}

if (benchmarkOptions.ShowHelp)
{
    BenchmarkOptions.PrintUsage();
    return 0;
}

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Services.AddSingleton<BenchmarkRecorder>();
builder.Services
    .AddBackoffBus(
        options =>
        {
            options.ProcessorConcurrency =
                benchmarkOptions.ConsumerConcurrency;
            options.DeadLetterProcessorConcurrency = 1;
        },
        typeof(BenchmarkIntegrationEvent).Assembly)
    .UseRabbitMqConsumer(options =>
    {
        options.ConnectionString = benchmarkOptions.ConnectionString;
        options.QueueName = benchmarkOptions.QueueName;
        options.PrefetchCount = benchmarkOptions.PrefetchCount;
        options.PublisherChannelCount =
            benchmarkOptions.PublisherChannelCount;
        options.PublisherBatchSize =
            benchmarkOptions.PublisherBatchSize;
        options.PublisherBatchMinimumSize =
            benchmarkOptions.PublisherBatchMinimumSize;
        options.PublisherBatchDelay =
            benchmarkOptions.PublisherBatchDelay;
        options.DelayBucketSelection =
            benchmarkOptions.DelayBucketSelection;
    });

using var host = builder.Build();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await host.StartAsync(cancellation.Token);

try
{
    var eventBus = host.Services.GetRequiredService<IEventBus>();
    var recorder = host.Services.GetRequiredService<BenchmarkRecorder>();
    var timeProvider = host.Services.GetRequiredService<TimeProvider>();

    PrintConfiguration(benchmarkOptions);

    if (benchmarkOptions.WarmupCount > 0)
    {
        Console.WriteLine(
            $"Warming up with {benchmarkOptions.WarmupCount} messages...");
        await RunAsync(
            eventBus,
            recorder,
            timeProvider,
            benchmarkOptions,
            benchmarkOptions.WarmupCount,
            cancellation.Token);
    }

    Console.WriteLine(
        $"Measuring {benchmarkOptions.Count} messages...");
    var result = await RunAsync(
        eventBus,
        recorder,
        timeProvider,
        benchmarkOptions,
        benchmarkOptions.Count,
        cancellation.Token);
    PrintResult(result);
}
finally
{
    await host.StopAsync(CancellationToken.None);
}

return 0;

static async Task<BenchmarkResult> RunAsync(
    IEventBus eventBus,
    BenchmarkRecorder recorder,
    TimeProvider timeProvider,
    BenchmarkOptions options,
    int count,
    CancellationToken cancellationToken)
{
    var runId = Guid.NewGuid();
    var run = recorder.BeginRun(runId, count);
    var publishLatencyMilliseconds = new double[count];
    var totalStopwatch = Stopwatch.StartNew();
    var publishStopwatch = Stopwatch.StartNew();

    await Parallel.ForEachAsync(
        Enumerable.Range(0, count),
        new ParallelOptions
        {
            MaxDegreeOfParallelism = options.PublisherConcurrency,
            CancellationToken = cancellationToken
        },
        async (sequence, currentCancellationToken) =>
        {
            var publishedAt = timeProvider.GetUtcNow();
            var integrationEvent = new BenchmarkIntegrationEvent(
                Guid.NewGuid(),
                runId,
                sequence,
                publishedAt)
            {
                ExecuteAfter = publishedAt.Add(
                    options.ScheduledDelay)
            };
            var startedAt = Stopwatch.GetTimestamp();

            await eventBus.PublishAsync(
                integrationEvent,
                currentCancellationToken);

            publishLatencyMilliseconds[sequence] =
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        });

    publishStopwatch.Stop();
    await run.WaitAsync(options.Timeout, cancellationToken);
    totalStopwatch.Stop();

    return new BenchmarkResult(
        count,
        publishStopwatch.Elapsed,
        totalStopwatch.Elapsed,
        publishLatencyMilliseconds,
        run.CreateSnapshot());
}

static void PrintConfiguration(BenchmarkOptions options)
{
    Console.WriteLine("BackoffBus RabbitMQ benchmark");
    Console.WriteLine($"  Queue:                 {options.QueueName}");
    Console.WriteLine($"  Scheduled delay:       {options.ScheduledDelay}");
    Console.WriteLine($"  Publisher concurrency: {options.PublisherConcurrency}");
    Console.WriteLine($"  Publisher channels:    {options.PublisherChannelCount}");
    Console.WriteLine($"  Publisher batch size:  {options.PublisherBatchSize}");
    Console.WriteLine($"  Publisher batch min:   {options.PublisherBatchMinimumSize}");
    Console.WriteLine($"  Publisher batch delay: {options.PublisherBatchDelay}");
    Console.WriteLine($"  Delay bucket:          {options.DelayBucketSelection}");
    Console.WriteLine($"  Consumer concurrency:  {options.ConsumerConcurrency}");
    Console.WriteLine($"  Prefetch:              {options.PrefetchCount}");
}

static void PrintResult(BenchmarkResult result)
{
    Console.WriteLine();
    Console.WriteLine("Results");
    Console.WriteLine(
        $"  Publish throughput:    {FormatRate(result.Count, result.PublishElapsed)} msg/s");
    Console.WriteLine(
        $"  Completion throughput: {FormatRate(result.Count, result.TotalElapsed)} msg/s");
    Console.WriteLine($"  Publish elapsed:       {result.PublishElapsed}");
    Console.WriteLine($"  Total elapsed:         {result.TotalElapsed}");
    PrintDistribution(
        "Publish latency",
        result.PublishLatencyMilliseconds);
    PrintDistribution(
        "End-to-end latency",
        result.Snapshot.EndToEndMilliseconds);
    PrintDistribution(
        "Schedule drift",
        result.Snapshot.ScheduleDriftMilliseconds);
    Console.WriteLine(
        $"  Early deliveries:      {result.Snapshot.ScheduleDriftMilliseconds.Count(value => value < 0)}");
    Console.WriteLine(
        $"  Duplicate deliveries:  {result.Snapshot.DuplicateCount}");
}

static void PrintDistribution(string name, double[] source)
{
    var values = source.Order().ToArray();
    Console.WriteLine(
        string.Create(
            CultureInfo.InvariantCulture,
            $"  {name,-22} min={values[0],8:F2} ms  p50={Percentile(values, 0.50),8:F2} ms  p95={Percentile(values, 0.95),8:F2} ms  p99={Percentile(values, 0.99),8:F2} ms  max={values[^1],8:F2} ms"));
}

static double Percentile(double[] sortedValues, double percentile)
{
    var index = Math.Max(
        0,
        (int)Math.Ceiling(percentile * sortedValues.Length) - 1);
    return sortedValues[index];
}

static string FormatRate(int count, TimeSpan elapsed) =>
    (count / elapsed.TotalSeconds).ToString(
        "F2",
        CultureInfo.InvariantCulture);

internal sealed record BenchmarkResult(
    int Count,
    TimeSpan PublishElapsed,
    TimeSpan TotalElapsed,
    double[] PublishLatencyMilliseconds,
    BenchmarkSnapshot Snapshot);
