using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using MsSqlServiceRegistry;

using ServiceRegistry.Abstractions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class ServiceRegistryServiceExtensions
{
    public static IServiceCollection AddServiceRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MsSqlServiceRegistryConfiguration>().Bind(configuration.GetSection("ServiceRegistry"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MsSqlServiceRegistryConfiguration>>().Value);

        services.AddHostedService<MsSqlServiceRegistryHeartbeatService>();
        services.AddSingleton<IServiceRegistry, MsSqlServiceRegistry.MsSqlServiceRegistry>();
        services.AddSingleton<IDataClient, DataClient>();

        return services;
    }
}