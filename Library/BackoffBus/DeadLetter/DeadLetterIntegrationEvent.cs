using BackoffBus.Abstractions;

namespace BackoffBus.DeadLetter;

/// <summary>
/// Describes an event that exhausted its delivery retries.
/// </summary>
public sealed record DeadLetterIntegrationEvent
{
    /// <summary>Creates a dead-letter event.</summary>
    /// <param name="integrationEvent">The event that could not be delivered.</param>
    /// <param name="retryCount">The number of completed retries.</param>
    /// <param name="exception">The last delivery exception.</param>
    /// <param name="failedAt">The time at which retries were exhausted.</param>
    public DeadLetterIntegrationEvent(
        IIntegrationEvent integrationEvent,
        int retryCount,
        Exception exception,
        DateTimeOffset failedAt)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

        IntegrationEvent = integrationEvent;
        RetryCount = retryCount;
        Exception = exception;
        FailedAt = failedAt;
    }

    /// <summary>Gets the event that could not be delivered.</summary>
    public IIntegrationEvent IntegrationEvent { get; }

    /// <summary>Gets the number of completed retries.</summary>
    public int RetryCount { get; }

    /// <summary>Gets the last delivery exception.</summary>
    public Exception Exception { get; }

    /// <summary>Gets the time at which retries were exhausted.</summary>
    public DateTimeOffset FailedAt { get; }
}
