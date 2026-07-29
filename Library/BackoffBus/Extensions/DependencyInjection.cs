using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.Serialization;
using BackoffBus.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace BackoffBus.Extensions;

/// <summary>Provides dependency-injection registration for BackoffBus.</summary>
public static class DependencyInjection
{
    /// <summary>Registers BackoffBus and scans event handler assemblies.</summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="assemblies">Assemblies containing events and handlers.</param>
    /// <returns>A builder used to select a queue provider.</returns>
    public static BackoffBusBuilder AddBackoffBus(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        AddBackoffBus(services, static _ => { }, assemblies);

    /// <summary>
    /// Registers and configures BackoffBus and scans event handler assemblies.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">A callback that configures BackoffBus.</param>
    /// <param name="assemblies">Assemblies containing events and handlers.</param>
    /// <returns>A builder used to select a queue provider.</returns>
    public static BackoffBusBuilder AddBackoffBus(
        this IServiceCollection services,
        Action<BackoffBusOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(assemblies);

        RegisterIntegrationEventHandlers(services, assemblies);
        IntegrationEventJsonSerializer.Register(assemblies);
        services.AddOptions<BackoffBusOptions>().Configure(configure);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddScoped<
            IIntegrationEventDispatcher,
            IntegrationEventDispatcher>();
        services.TryAddSingleton<
            IDeadLetterIntegrationEventHandler,
            LoggingDeadLetterIntegrationEventHandler>();
        return new BackoffBusBuilder(services);
    }

    private static void RegisterIntegrationEventHandlers(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var implementationType in GetLoadableTypes(assembly))
            {
                if (implementationType is
                    {
                        IsClass: true,
                        IsAbstract: false,
                        ContainsGenericParameters: false
                    })
                {
                    RegisterHandlerInterfaces(
                        services,
                        implementationType);
                }
            }
        }
    }

    private static void RegisterHandlerInterfaces(
        IServiceCollection services,
        Type implementationType)
    {
        foreach (var serviceType in implementationType.GetInterfaces())
        {
            if (serviceType.IsGenericType
                && serviceType.GetGenericTypeDefinition()
                == typeof(IIntegrationEventHandler<>))
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Transient(
                        serviceType,
                        implementationType));
            }
        }
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
}
