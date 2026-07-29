using BackoffBus.Abstractions;
using BackoffBus.Configuration;
using BackoffBus.InMemory.Configuration;
using BackoffBus.Job;
using BackoffBus.Queue;
using BackoffBus.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BackoffBus.Extensions;

/// <summary>
/// Provides registration for the BackoffBus in-memory provider.
/// </summary>
public static class InMemoryBackoffBusBuilderExtensions
{
    /// <summary>Uses in-memory queues with their default capacities.</summary>
    public static BackoffBusBuilder UseInMemory(
        this BackoffBusBuilder builder) =>
        UseInMemory(builder, static _ => { });

    /// <summary>Uses in-memory queues with custom capacities.</summary>
    public static BackoffBusBuilder UseInMemory(
        this BackoffBusBuilder builder,
        Action<InMemoryBackoffBusOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        builder.UseProvider("InMemory");

        builder.Services
            .AddOptions<InMemoryBackoffBusOptions>()
            .Configure(configure);
        builder.Services.TryAddSingleton<InMemoryIntegrationEventQueue>();
        builder.Services.TryAddSingleton<IEventBus, EventBus>();
        builder.Services.AddHostedService<IntegrationEventScheduler>();
        builder.Services.AddHostedService<IntegrationEventProcessorJob>();
        builder.Services.AddHostedService<
            DeadLetterIntegrationEventProcessorJob>();
        return builder;
    }
}
