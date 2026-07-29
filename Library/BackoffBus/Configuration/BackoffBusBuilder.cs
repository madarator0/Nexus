using Microsoft.Extensions.DependencyInjection;

namespace BackoffBus.Configuration;

/// <summary>
/// Configures a BackoffBus queue provider.
/// </summary>
public sealed class BackoffBusBuilder
{
    internal BackoffBusBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>Gets the application service collection.</summary>
    public IServiceCollection Services { get; }

    internal void UseProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var configuredProvider = Services
            .LastOrDefault(
                static descriptor =>
                    descriptor.ServiceType
                    == typeof(BackoffBusProviderMarker))
            ?.ImplementationInstance as BackoffBusProviderMarker;

        if (configuredProvider is not null)
        {
            throw new InvalidOperationException(
                $"BackoffBus provider '{configuredProvider.Name}' is already configured.");
        }

        Services.AddSingleton(
            new BackoffBusProviderMarker(providerName));
    }

    private sealed record BackoffBusProviderMarker(string Name);
}
