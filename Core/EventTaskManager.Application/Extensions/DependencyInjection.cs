using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace EventTaskManager.Application.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddAppEvents(this IServiceCollection services)
    {
        services.AddMediatR(Assembly.GetExecutingAssembly());
        return services;
    }
}
