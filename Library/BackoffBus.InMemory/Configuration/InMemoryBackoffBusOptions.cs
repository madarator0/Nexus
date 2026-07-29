namespace BackoffBus.InMemory.Configuration;

/// <summary>Configures the capacities of the in-memory queues.</summary>
public sealed class InMemoryBackoffBusOptions
{
    /// <summary>Gets or sets scheduled-event queue capacity.</summary>
    public int IncomingQueueCapacity { get; set; } = 50_000;

    /// <summary>Gets or sets ready-event queue capacity.</summary>
    public int ReadyQueueCapacity { get; set; } = 50_000;

    /// <summary>Gets or sets dead-letter queue capacity.</summary>
    public int DeadLetterQueueCapacity { get; set; } = 10_000;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            IncomingQueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            ReadyQueueCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            DeadLetterQueueCapacity);
    }
}
