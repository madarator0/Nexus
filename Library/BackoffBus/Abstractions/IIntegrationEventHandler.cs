namespace BackoffBus.Abstractions;

/// <summary>Handles a concrete integration event.</summary>
/// <typeparam name="TEvent">The integration event type.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    /// <summary>Handles an integration event.</summary>
    /// <param name="integrationEvent">The event to handle.</param>
    /// <param name="cancellationToken">
    /// Cancels integration event handling.
    /// </param>
    ValueTask HandleAsync(
        TEvent integrationEvent,
        CancellationToken cancellationToken);
}
