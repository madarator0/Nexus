using BackoffBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BackoffBus.Sandbox.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddSandboxEvents(this IServiceCollection services)
    {
        services.AddBackoffBus(Assembly.GetExecutingAssembly());
        return services;
    }
}
