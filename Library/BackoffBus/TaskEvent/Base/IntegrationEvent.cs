using BackoffBus.Abstractions;

namespace BackoffBus.TaskEvent.Base;

/// <summary>
/// Provides immutable scheduling and retry defaults for integration events.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <summary>Creates an integration event for immediate delivery.</summary>
    /// <param name="id">The event identifier.</param>
    protected IntegrationEvent(Guid id)
    {
        Id = id;
    }

    /// <summary>Gets the event identifier.</summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the earliest delivery time. It can only be assigned while the
    /// event is being constructed.
    /// </summary>
    public DateTimeOffset ExecuteAfter { get; init; } =
        DateTimeOffset.UtcNow;

    /// <summary>Gets the retry limit defined by the concrete event type.</summary>
    public abstract int MaxRetries { get; }
}
