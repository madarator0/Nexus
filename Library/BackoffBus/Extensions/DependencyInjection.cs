using BackoffBus.Abstractions;
using BackoffBus.Job;
using BackoffBus.Queue;
using BackoffBus.Serialization;
using BackoffBus.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BackoffBus.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddBackoffBus(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddMediatR(assemblies);
        IntegrationEventJsonSerializer.Register(assemblies);
        services.AddSingleton<InMemoryTaskEventQueue>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddHostedService<IntegrationEventScheduler>();
        services.AddHostedService<IntegrationEventProcessorJob>();
        services.AddHostedService<DeadLetterIntegrationEventProcessorJob>();
        return services;
    }
}
