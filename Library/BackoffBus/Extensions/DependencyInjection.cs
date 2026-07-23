using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.DeadLetter;
using BackoffBus.Job;
using BackoffBus.Queue;
using BackoffBus.Serialization;
using BackoffBus.Services;
using MediatR;
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
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddBackoffBus(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        AddBackoffBus(services, static _ => { }, assemblies);

    /// <summary>
    /// Registers and configures BackoffBus and scans event handler assemblies.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">A callback that configures BackoffBus.</param>
    /// <param name="assemblies">Assemblies containing events and handlers.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddBackoffBus(
        this IServiceCollection services,
        Action<BackoffBusOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(assemblies);

        services.AddMediatR(assemblies);
        IntegrationEventJsonSerializer.Register(assemblies);
        services.AddOptions<BackoffBusOptions>().Configure(configure);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<InMemoryTaskEventQueue>();
        services.TryAddSingleton<IEventBus, EventBus>();
        services.TryAddSingleton<
            IDeadLetterIntegrationEventHandler,
            LoggingDeadLetterIntegrationEventHandler>();
        services.AddHostedService<IntegrationEventScheduler>();
        services.AddHostedService<IntegrationEventProcessorJob>();
        services.AddHostedService<DeadLetterIntegrationEventProcessorJob>();
        return services;
    }
}
