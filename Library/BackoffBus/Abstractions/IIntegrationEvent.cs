namespace BackoffBus.Abstractions;

/// <summary>
/// Defines an immutable integration event and its initial delivery policy.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Gets the event identifier.</summary>
    Guid Id { get; }

    /// <summary>Gets the earliest time at which the event may be delivered.</summary>
    DateTimeOffset ExecuteAfter { get; }

    /// <summary>Gets the number of retries allowed after initial delivery.</summary>
    int MaxRetries { get; }
}
