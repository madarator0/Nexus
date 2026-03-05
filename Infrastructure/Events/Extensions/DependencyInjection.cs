using Events.Job;
using Events.Queue;
using Events.Services;
using EventTaskManager.Application.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Events.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddEvents(this IServiceCollection services)
        {
            services.AddSingleton<InMemoryTaskEventQueue>();
            services.AddSingleton<IEventBus, EventBus>();
            services.AddHostedService<IntegrationEventScheduler>();
            services.AddHostedService<IntegrationEventProcessorJob>();
            return services;
        }
    }
}
