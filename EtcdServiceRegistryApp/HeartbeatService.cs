using EtcdServiceRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtcdServiceRegistryApp;

public class HeartbeatService : BackgroundService
{
    private readonly ServiceRegistry _registry;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(ServiceRegistry registry, ILogger<HeartbeatService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        _logger.LogInformation("Heartbeat service running");

        try
        {
            await _registry.SendHeartbeatsAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("A task/operation cancelled exception was caught.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while sending heartbeats. Un-registering service");
        }

        await _registry.UnregisterServiceAsync(CancellationToken.None);
        
        _logger.LogInformation("Heartbeat service stopped");
    }
}