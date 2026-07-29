namespace BackoffBus.RabbitMQ.Configuration;

/// <summary>
/// Controls how a broker-managed delay bucket is selected.
/// </summary>
public enum RabbitMqDelayBucketSelection
{
    /// <summary>
    /// Uses the smallest bucket that covers the remaining delay. This
    /// minimizes broker hops but can deliver up to one bucket step late.
    /// </summary>
    Ceiling,

    /// <summary>
    /// Uses the largest bucket within the remaining delay. This improves
    /// precision but can require multiple broker hops.
    /// </summary>
    Floor
}
