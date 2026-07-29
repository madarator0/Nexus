namespace BackoffBus.Configuration;

/// <summary>Configures delivery concurrency and retry timing.</summary>
public sealed class BackoffBusOptions
{
    /// <summary>Gets or sets maximum concurrent event deliveries.</summary>
    public int ProcessorConcurrency { get; set; } = 5;

    /// <summary>Gets or sets maximum concurrent dead-letter handlers.</summary>
    public int DeadLetterProcessorConcurrency { get; set; } = 2;

    /// <summary>Gets or sets delay before the first retry.</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the retry-delay upper bound.</summary>
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Validates the configured values.</summary>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ProcessorConcurrency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(DeadLetterProcessorConcurrency);

        if (InitialRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialRetryDelay),
                "Initial retry delay must be positive.");
        }

        if (MaximumRetryDelay < InitialRetryDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumRetryDelay),
                "Maximum retry delay must be greater than or equal to the initial retry delay.");
        }
    }
}
