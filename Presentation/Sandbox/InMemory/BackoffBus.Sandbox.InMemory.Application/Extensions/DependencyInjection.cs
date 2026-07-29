using BackoffBus.Configuration;
using BackoffBus.Extensions;
using BackoffBus.Sandbox.IntegrationEvents.Email;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BackoffBus.Sandbox.InMemory.Application.Extensions;

public static class DependencyInjection
{
    public static BackoffBusBuilder AddInMemorySandboxApplication(
        this IServiceCollection services) =>
        services.AddBackoffBus(
            Assembly.GetExecutingAssembly(),
            typeof(EmailDeliveryRequestedIntegrationEvent).Assembly);
}
