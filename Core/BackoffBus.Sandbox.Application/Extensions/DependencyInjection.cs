using BackoffBus.Configuration;
using BackoffBus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BackoffBus.Sandbox.Application.Extensions;

public static class DependencyInjection
{
    public static BackoffBusBuilder AddSandboxEvents(
        this IServiceCollection services) =>
        services.AddBackoffBus(Assembly.GetExecutingAssembly());
}
