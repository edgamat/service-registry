using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public class MsSqlServiceRegistryHeartbeatService : BackgroundService
{
    private readonly IServiceRegistry _registry;
    private readonly ILogger<MsSqlServiceRegistryHeartbeatService> _logger;

    public MsSqlServiceRegistryHeartbeatService(IServiceRegistry registry, ILogger<MsSqlServiceRegistryHeartbeatService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _registry.RegisterServiceAsync(CancellationToken.None);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _registry.UnregisterServiceAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
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

        _logger.LogInformation("Heartbeat service stopped");
    }
}