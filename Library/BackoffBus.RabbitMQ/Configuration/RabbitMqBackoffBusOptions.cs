namespace BackoffBus.RabbitMQ.Configuration;

/// <summary>Configures the RabbitMQ provider.</summary>
public sealed class RabbitMqBackoffBusOptions
{
    /// <summary>Gets or sets the AMQP connection URI.</summary>
    public string ConnectionString { get; set; } =
        "amqp://guest:guest@localhost:5672/";

    /// <summary>Gets or sets the durable queue name.</summary>
    public string QueueName { get; set; } = "backoff-bus";

    /// <summary>
    /// Gets or sets the maximum number of unacknowledged deliveries.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 32;

    /// <summary>Gets or sets whether messages survive broker restarts.</summary>
    public bool PersistentMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of independent publisher channels.
    /// </summary>
    public int PublisherChannelCount { get; set; } = 4;

    /// <summary>
    /// Gets or sets the bounded capacity shared by publisher workers.
    /// </summary>
    public int PublisherQueueCapacity { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of outstanding publishes grouped
    /// by each publisher worker.
    /// </summary>
    public int PublisherBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the minimum group size that uses pipelined confirms.
    /// Smaller groups are confirmed individually.
    /// </summary>
    public int PublisherBatchMinimumSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets how long a publisher worker waits to fill a batch.
    /// </summary>
    public TimeSpan PublisherBatchDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets how scheduled messages select a delay bucket.
    /// </summary>
    public RabbitMqDelayBucketSelection DelayBucketSelection { get; set; } =
        RabbitMqDelayBucketSelection.Ceiling;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(QueueName);
        ArgumentOutOfRangeException.ThrowIfZero(PrefetchCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            PublisherChannelCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            PublisherQueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            PublisherBatchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            PublisherBatchMinimumSize);

        if (PublisherBatchMinimumSize > PublisherBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PublisherBatchMinimumSize),
                "Publisher batch minimum size cannot exceed its maximum size.");
        }

        if (PublisherBatchDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PublisherBatchDelay),
                "Publisher batch delay cannot be negative.");
        }

        if (!Enum.IsDefined(DelayBucketSelection))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DelayBucketSelection));
        }

        if (!Uri.TryCreate(
                ConnectionString,
                UriKind.Absolute,
                out var connectionUri)
            || connectionUri.Scheme is not ("amqp" or "amqps"))
        {
            throw new ArgumentException(
                "RabbitMQ connection string must be an absolute amqp or amqps URI.",
                nameof(ConnectionString));
        }
    }
}
