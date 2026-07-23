using BackoffBus.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace BackoffBus.Serialization;

/// <summary>
/// Serializes registered integration event types using stable discriminators.
/// </summary>
public static class IntegrationEventJsonSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions =
        CreateDefaultOptions();
    private static readonly ConcurrentDictionary<string, Type>
        TypesByDiscriminator = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<
        Type,
        IntegrationEventDescriptor> DescriptorsByType = new();

    /// <summary>Registers an integration event type.</summary>
    /// <typeparam name="T">The concrete event type.</typeparam>
    public static void Register<T>()
        where T : class, IIntegrationEvent =>
        Register(typeof(T));

    /// <summary>Registers explicit integration event types.</summary>
    /// <param name="integrationEventTypes">The types to register.</param>
    public static void Register(params Type[] integrationEventTypes)
    {
        ArgumentNullException.ThrowIfNull(integrationEventTypes);

        foreach (var integrationEventType in integrationEventTypes)
        {
            ArgumentNullException.ThrowIfNull(integrationEventType);
            Register(integrationEventType);
        }
    }

    /// <summary>
    /// Registers all concrete integration event types in the assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public static void Register(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var integrationEventType in GetLoadableTypes(assembly))
            {
                if (IsSupportedIntegrationEventType(integrationEventType))
                {
                    Register(integrationEventType);
                }
            }
        }
    }

    /// <summary>Serializes an integration event.</summary>
    /// <param name="integrationEvent">The event to serialize.</param>
    /// <returns>The serialized envelope.</returns>
    public static string Serialize(IIntegrationEvent integrationEvent) =>
        Serialize(integrationEvent, DefaultOptions);

    /// <summary>Serializes an integration event with custom JSON options.</summary>
    /// <param name="integrationEvent">The event to serialize.</param>
    /// <param name="options">The JSON options to use.</param>
    /// <returns>The serialized envelope.</returns>
    public static string Serialize(
        IIntegrationEvent integrationEvent,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(options);

        var integrationEventType = integrationEvent.GetType();
        var descriptor = Register(integrationEventType);
        var envelope = new IntegrationEventEnvelope(
            descriptor.Name,
            descriptor.Version,
            JsonSerializer.SerializeToElement(
                integrationEvent,
                integrationEventType,
                options));

        return JsonSerializer.Serialize(envelope, options);
    }

    /// <summary>Deserializes a registered integration event.</summary>
    /// <param name="json">The serialized envelope.</param>
    /// <param name="assemblies">
    /// Optional assemblies whose event types are registered first.
    /// </param>
    /// <returns>The deserialized event.</returns>
    public static IIntegrationEvent Deserialize(
        string json,
        params Assembly[] assemblies) =>
        Deserialize(json, DefaultOptions, assemblies);

    /// <summary>
    /// Deserializes a registered integration event with custom JSON options.
    /// </summary>
    /// <param name="json">The serialized envelope.</param>
    /// <param name="options">The JSON options to use.</param>
    /// <param name="assemblies">
    /// Optional assemblies whose event types are registered first.
    /// </param>
    /// <returns>The deserialized event.</returns>
    public static IIntegrationEvent Deserialize(
        string json,
        JsonSerializerOptions options,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(assemblies);

        if (assemblies.Length > 0)
        {
            Register(assemblies);
        }

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(
                json,
                options)
            ?? throw new JsonException("Integration event json is empty.");

        if (string.IsNullOrWhiteSpace(envelope.Name))
        {
            throw new JsonException(
                "Integration event name is not specified.");
        }

        if (envelope.Version <= 0)
        {
            throw new JsonException(
                "Integration event version must be positive.");
        }

        if (envelope.Payload.ValueKind
            is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new JsonException(
                "Integration event payload is not specified.");
        }

        var integrationEventType = ResolveType(
            envelope.Name,
            envelope.Version);
        var integrationEvent = envelope.Payload.Deserialize(
            integrationEventType,
            options);

        return integrationEvent as IIntegrationEvent
            ?? throw new JsonException(
                $"Type '{integrationEventType.FullName}' does not implement '{nameof(IIntegrationEvent)}'.");
    }

    /// <summary>Deserializes a registered integration event as a known type.</summary>
    /// <typeparam name="T">The expected event type.</typeparam>
    /// <param name="json">The serialized envelope.</param>
    /// <param name="assemblies">
    /// Optional assemblies whose event types are registered first.
    /// </param>
    /// <returns>The deserialized event.</returns>
    public static T Deserialize<T>(
        string json,
        params Assembly[] assemblies)
        where T : class, IIntegrationEvent =>
        Deserialize<T>(json, DefaultOptions, assemblies);

    /// <summary>
    /// Deserializes a registered integration event as a known type with
    /// custom JSON options.
    /// </summary>
    /// <typeparam name="T">The expected event type.</typeparam>
    /// <param name="json">The serialized envelope.</param>
    /// <param name="options">The JSON options to use.</param>
    /// <param name="assemblies">
    /// Optional assemblies whose event types are registered first.
    /// </param>
    /// <returns>The deserialized event.</returns>
    public static T Deserialize<T>(
        string json,
        JsonSerializerOptions options,
        params Assembly[] assemblies)
        where T : class, IIntegrationEvent
    {
        var integrationEvent = Deserialize(json, options, assemblies);

        return integrationEvent as T
            ?? throw new JsonException(
                $"Integration event json does not contain '{typeof(T).FullName}'.");
    }

    private static IntegrationEventDescriptor Register(
        Type integrationEventType)
    {
        ValidateIntegrationEventType(integrationEventType);

        return DescriptorsByType.GetOrAdd(
            integrationEventType,
            currentType =>
            {
                var attribute = currentType.GetCustomAttribute<
                    IntegrationEventAttribute>();
                var descriptor = attribute is null
                    ? CreateDefaultDescriptor(currentType)
                    : new IntegrationEventDescriptor(
                        attribute.Name,
                        attribute.Version);
                var key = BuildKey(
                    descriptor.Name,
                    descriptor.Version);

                if (TypesByDiscriminator.TryAdd(key, currentType))
                {
                    return descriptor;
                }

                var existingType = TypesByDiscriminator[key];

                if (existingType != currentType)
                {
                    throw new InvalidOperationException(
                        $"Integration event discriminator conflict for '{descriptor.Name}' version {descriptor.Version}.");
                }

                return descriptor;
            });
    }

    private static IntegrationEventDescriptor CreateDefaultDescriptor(
        Type integrationEventType)
    {
        var assemblyName = integrationEventType.Assembly.GetName().Name
            ?? throw new InvalidOperationException(
                $"Assembly name for '{integrationEventType.FullName}' is not available.");
        var typeName = integrationEventType.FullName
            ?? throw new InvalidOperationException(
                "Integration event type must have a full name.");

        return new IntegrationEventDescriptor(
            $"{assemblyName}:{typeName}",
            1);
    }

    private static Type ResolveType(string name, int version)
    {
        var key = BuildKey(name, version);

        if (TypesByDiscriminator.TryGetValue(
                key,
                out var registeredType))
        {
            return registeredType;
        }

        throw new JsonException(
            $"Integration event '{name}' version {version} is not registered.");
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(
                static type => type is not null)!;
        }
    }

    private static bool IsSupportedIntegrationEventType(Type type) =>
        type is
        {
            IsClass: true,
            IsAbstract: false,
            ContainsGenericParameters: false
        }
        && typeof(IIntegrationEvent).IsAssignableFrom(type);

    private static void ValidateIntegrationEventType(
        Type integrationEventType)
    {
        ArgumentNullException.ThrowIfNull(integrationEventType);

        if (!IsSupportedIntegrationEventType(integrationEventType))
        {
            throw new ArgumentException(
                $"Type '{integrationEventType.FullName}' must be a non-abstract class that implements '{nameof(IIntegrationEvent)}'.",
                nameof(integrationEventType));
        }
    }

    private static string BuildKey(string name, int version) =>
        $"{version}:{name}";

    private static JsonSerializerOptions CreateDefaultOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private sealed record IntegrationEventEnvelope(
        string Name,
        int Version,
        JsonElement Payload);

    private sealed record IntegrationEventDescriptor(
        string Name,
        int Version);
}
