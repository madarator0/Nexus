namespace BackoffBus.Serialization;

/// <summary>
/// Assigns a stable serialized name and schema version to an event type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventAttribute : Attribute
{
    /// <summary>Creates an integration event discriminator.</summary>
    /// <param name="name">A stable name independent of the CLR type name.</param>
    /// <param name="version">The positive event schema version.</param>
    public IntegrationEventAttribute(string name, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Integration event name cannot be empty.",
                nameof(name));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        Name = name;
        Version = version;
    }

    /// <summary>Gets the stable event name.</summary>
    public string Name { get; }

    /// <summary>Gets the event schema version.</summary>
    public int Version { get; }
}
