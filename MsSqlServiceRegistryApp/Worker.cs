using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistryApp;

public class Worker : BackgroundService
{
    private readonly IServiceRegistry _registry;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceRegistry registry, ILogger<Worker> logger)
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
                var index = Array.FindIndex(instances, item => item.Id == thisInstance.Id);
                
                _logger.LogInformation("This instance is alive: {InstanceId}, Index {Index}", thisInstance.Id, index);
            }

            // if (await _registry.IsLeaderAsync(stoppingToken))
            // {
            //     _logger.LogInformation("This instance is the leader: {InstanceId}", thisInstance?.Id);
            // }

            await Task.Delay(5_000, stoppingToken);
        }
    }
}