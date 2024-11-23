using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MsSqlServiceRegistry;

public static class ServiceRegistryServiceExtensions
{
    public static IServiceCollection AddServiceRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MsSqlServiceRegistryConfiguration>().Bind(configuration.GetSection("ServiceRegistry"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MsSqlServiceRegistryConfiguration>>().Value);

        services.AddHostedService<MsSqlServiceRegistryHeartbeatService>();
        services.AddSingleton<MsSqlServiceRegistry>();

        return services;
    }
}