using Events.Abstractions;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Events.Serialization;

public static class IntegrationEventJsonSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();
    private static readonly ConcurrentDictionary<string, Type> TypesByKey = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Type, IntegrationEventDescriptor> DescriptorsByType = new();

    public static void Register<T>()
        where T : class, IIntegrationEvent =>
        Register(typeof(T));

    public static void Register(params Type[] integrationEventTypes)
    {
        ArgumentNullException.ThrowIfNull(integrationEventTypes);

        foreach (var integrationEventType in integrationEventTypes)
        {
            ArgumentNullException.ThrowIfNull(integrationEventType);
            Register(integrationEventType);
        }
    }

    public static void Register(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var integrationEventType in GetLoadableTypes(assembly))
            {
                if (!IsSupportedIntegrationEventType(integrationEventType))
                {
                    continue;
                }

                Register(integrationEventType);
            }
        }
    }

    public static string Serialize(IIntegrationEvent integrationEvent) =>
        Serialize(integrationEvent, DefaultOptions);

    public static string Serialize(
        IIntegrationEvent integrationEvent,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(options);

        var integrationEventType = integrationEvent.GetType();
        var descriptor = Register(integrationEventType);

        var envelope = new IntegrationEventEnvelope(
            descriptor.AssemblyName,
            descriptor.TypeName,
            JsonSerializer.SerializeToElement(integrationEvent, integrationEventType, options));

        return JsonSerializer.Serialize(envelope, options);
    }

    public static IIntegrationEvent Deserialize(string json, params Assembly[] assemblies) =>
        Deserialize(json, DefaultOptions, assemblies);

    public static IIntegrationEvent Deserialize(
        string json,
        JsonSerializerOptions options,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(options);

        if (assemblies.Length > 0)
        {
            Register(assemblies);
        }

        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(json, options)
            ?? throw new JsonException("Integration event json is empty.");

        if (string.IsNullOrWhiteSpace(envelope.Assembly))
        {
            throw new JsonException("Integration event assembly is not specified.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Type))
        {
            throw new JsonException("Integration event type is not specified.");
        }

        if (envelope.Payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new JsonException("Integration event payload is not specified.");
        }

        var integrationEventType = ResolveType(envelope.Assembly, envelope.Type, assemblies);
        var integrationEvent = envelope.Payload.Deserialize(integrationEventType, options);

        return integrationEvent as IIntegrationEvent
            ?? throw new JsonException(
                $"Type '{integrationEventType.FullName}' does not implement '{nameof(IIntegrationEvent)}'.");
    }

    public static T Deserialize<T>(string json, params Assembly[] assemblies)
        where T : class, IIntegrationEvent =>
        Deserialize<T>(json, DefaultOptions, assemblies);

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

    private static IntegrationEventDescriptor Register(Type integrationEventType)
    {
        ValidateIntegrationEventType(integrationEventType);

        return DescriptorsByType.GetOrAdd(
            integrationEventType,
            currentType =>
            {
                var assemblyName = currentType.Assembly.GetName().Name
                    ?? throw new InvalidOperationException(
                        $"Assembly name for '{currentType.FullName}' is not available.");

                var typeName = currentType.FullName
                    ?? throw new InvalidOperationException("Integration event type must have a full name.");

                var descriptor = new IntegrationEventDescriptor(assemblyName, typeName);
                var key = BuildKey(descriptor.AssemblyName, descriptor.TypeName);

                if (TypesByKey.TryAdd(key, currentType))
                {
                    return descriptor;
                }

                var existingType = TypesByKey[key];

                if (existingType != currentType)
                {
                    throw new InvalidOperationException(
                        $"Integration event discriminator conflict for '{key}'.");
                }

                return descriptor;
            });
    }

    private static Type ResolveType(
        string assemblyName,
        string typeName,
        Assembly[] assemblies)
    {
        var key = BuildKey(assemblyName, typeName);

        if (TypesByKey.TryGetValue(key, out var registeredType))
        {
            return registeredType;
        }

        foreach (var assembly in assemblies)
        {
            if (TryResolveFromAssembly(assembly, assemblyName, typeName, out var resolvedType))
            {
                Register(resolvedType);
                return resolvedType;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryResolveFromAssembly(assembly, assemblyName, typeName, out var resolvedType))
            {
                Register(resolvedType);
                return resolvedType;
            }
        }

        var assemblyQualifiedTypeName = $"{typeName}, {assemblyName}";
        var dynamicallyResolvedType = Type.GetType(assemblyQualifiedTypeName, throwOnError: false);

        if (dynamicallyResolvedType is not null)
        {
            Register(dynamicallyResolvedType);
            return dynamicallyResolvedType;
        }

        throw new JsonException(
            $"Integration event type '{typeName}' from assembly '{assemblyName}' is not registered.");
    }

    private static bool TryResolveFromAssembly(
        Assembly assembly,
        string assemblyName,
        string typeName,
        out Type integrationEventType)
    {
        integrationEventType = null!;

        if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
        {
            return false;
        }

        integrationEventType = assembly.GetType(typeName, throwOnError: false, ignoreCase: false)!;
        return integrationEventType is not null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static type => type is not null)!;
        }
    }

    private static bool IsSupportedIntegrationEventType(Type type) =>
        type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(IIntegrationEvent).IsAssignableFrom(type);

    private static void ValidateIntegrationEventType(Type integrationEventType)
    {
        ArgumentNullException.ThrowIfNull(integrationEventType);

        if (!IsSupportedIntegrationEventType(integrationEventType))
        {
            throw new ArgumentException(
                $"Type '{integrationEventType.FullName}' must be a non-abstract class that implements '{nameof(IIntegrationEvent)}'.",
                nameof(integrationEventType));
        }
    }

    private static string BuildKey(string assemblyName, string typeName) =>
        $"{assemblyName}:{typeName}";

    private static JsonSerializerOptions CreateDefaultOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private sealed record IntegrationEventEnvelope(
        string Assembly,
        string Type,
        JsonElement Payload);

    private sealed record IntegrationEventDescriptor(
        string AssemblyName,
        string TypeName);
}
