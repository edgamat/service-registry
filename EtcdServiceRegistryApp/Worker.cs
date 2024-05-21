using EtcdServiceRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtcdServiceRegistryApp;

public class Worker : BackgroundService
{
    private readonly ServiceRegistry _registry;
    private readonly ILogger<Worker> _logger;

    public Worker(ServiceRegistry registry, ILogger<Worker> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var instances = await _registry.GetRegisteredServicesAsync(stoppingToken);

            _logger.LogInformation("Instances: {InstanceCount}", instances.Length);

            var thisInstance = instances.FirstOrDefault(x => x.IsThisInstance);
            if (thisInstance != null)
            {
                _logger.LogInformation("This instance is alive: {InstanceId}", thisInstance.Id);
            }

            await Task.Delay(5_000, stoppingToken);
        }
    }
}