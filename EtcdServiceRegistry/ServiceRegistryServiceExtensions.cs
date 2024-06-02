using dotnet_etcd;
using dotnet_etcd.interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EtcdServiceRegistry;

public static class ServiceRegistryServiceExtensions
{
    public static IServiceCollection AddServiceRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceRegistryConfiguration>().Bind(configuration.GetSection("ServiceRegistry"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServiceRegistryConfiguration>>().Value);

        services.AddHostedService<ServiceRegistryHeartbeatService>();

        services.AddSingleton<IEtcdClient>(sp =>
        {
            var connectionString = sp.GetRequiredService<IOptions<ServiceRegistryConfiguration>>().Value.ConnectionString;
            return new EtcdClient(connectionString);
        });
        services.AddSingleton<ServiceRegistry>();

        return services;
    }

    public static void UseServiceRegistry(this IHost app)
    {
        var registry = app.Services.GetRequiredService<ServiceRegistry>();

        registry.RegisterServiceAsync(CancellationToken.None).Wait();

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        lifetime.ApplicationStopped.Register(() =>
        {
            registry.UnregisterServiceAsync(CancellationToken.None).Wait();
        });
    }
}