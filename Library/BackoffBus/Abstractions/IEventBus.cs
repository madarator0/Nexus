namespace BackoffBus.Abstractions;

/// <summary>
/// Enqueues integration events for scheduled in-process delivery.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Enqueues an integration event and waits until bounded queue capacity
    /// is available.
    /// </summary>
    /// <typeparam name="T">The concrete integration event type.</typeparam>
    /// <param name="integrationEvent">The event to enqueue.</param>
    /// <param name="cancellationToken">
    /// Cancels waiting for queue capacity.
    /// </param>
    ValueTask PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : class, IIntegrationEvent;
}
