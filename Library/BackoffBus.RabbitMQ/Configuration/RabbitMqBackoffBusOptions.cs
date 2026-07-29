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

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(QueueName);
        ArgumentOutOfRangeException.ThrowIfZero(PrefetchCount);

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
