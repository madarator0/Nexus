using Events.Abstractions;
using Events.Job;
using Events.Queue;
using Events.Serialization;
using Events.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Events.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddEvents(this IServiceCollection services, params Assembly[] assemblies)
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
