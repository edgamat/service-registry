using Microsoft.Extensions.Logging;
using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public class MsSqlServiceRegistry : IServiceRegistry
{
    private readonly MsSqlServiceRegistryConfiguration _configuration;
    private readonly IDataContext _context;
    private readonly ILogger<MsSqlServiceRegistry> _logger;

    private readonly string _instanceId;
    private readonly string _serviceName;
    private readonly string _serviceAddress;
    private Guid _leaseId;
    
    public MsSqlServiceRegistry(MsSqlServiceRegistryConfiguration configuration, IDataContext context, ILogger<MsSqlServiceRegistry> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        
        _instanceId = Guid.NewGuid().ToString();
        _serviceName = configuration.ServiceName;
        _serviceAddress = configuration.ServiceAddress;
    }
    
    public async Task RegisterServiceAsync(CancellationToken token)
    {
        // Create a lease that expires after 30 seconds
        _leaseId = await _context.LeaseGrantAsync(token); 

        var serviceInstanceInfo = new 
        {
            Id = _instanceId,
            Name = _serviceName,
            Address = AddressResolver.Resolve(_serviceAddress),
        };

        // Register instance
        await _context.RegisterInstanceAsync(_serviceName, _serviceAddress, _instanceId, _leaseId, token);
        
        _logger.LogInformation("Successfully registered service {@ServiceInstanceInfo} with lease {LeaseId}", serviceInstanceInfo, _leaseId);
    }

    public Task<bool> IsLeaderAsync(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public async Task SendHeartbeatsAsync(CancellationToken token)
    {
        _logger.LogInformation("Sending Heartbeats");
        
        while (!token.IsCancellationRequested)
        {
            await _context.LeaseKeepAliveAsync(_leaseId, token);
            
            _logger.LogInformation("Lease {LeaseId} kept alive", _leaseId);

            var delay = new TimeSpan(_configuration.TimeToLiveSeconds.Ticks / 2);
            
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    public async Task<ServiceInstance[]> GetRegisteredServicesAsync(CancellationToken token)
    {
        return await _context.GetServiceInstancesAsync(_serviceName, _instanceId, token);
    }

    public async Task UnregisterServiceAsync(CancellationToken token)
    {
        await _context.LeaseRevokeAsync(_leaseId, token);
    }
}