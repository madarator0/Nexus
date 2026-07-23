using BackoffBus.DeadLetter;

namespace BackoffBus.Abstractions;

/// <summary>
/// Handles integration events whose retry limit has been exhausted.
/// </summary>
public interface IDeadLetterIntegrationEventHandler
{
    /// <summary>Handles a dead-letter event.</summary>
    /// <param name="deadLetterEvent">The exhausted event and failure details.</param>
    /// <param name="cancellationToken">Cancels dead-letter handling.</param>
    ValueTask HandleAsync(
        DeadLetterIntegrationEvent deadLetterEvent,
        CancellationToken cancellationToken);
}
