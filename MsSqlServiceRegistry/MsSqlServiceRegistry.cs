using System.Text.Json;

using Microsoft.Extensions.Logging;

using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public class MsSqlServiceRegistry : IServiceRegistry
{
    private readonly MsSqlServiceRegistryConfiguration _configuration;
    private readonly IDataClient _client;
    private readonly ILogger<MsSqlServiceRegistry> _logger;

    private readonly string _instanceId;
    private readonly string _serviceName;
    private readonly string _serviceAddress;
    private long _leaseId;

    public MsSqlServiceRegistry(MsSqlServiceRegistryConfiguration configuration, IDataClient client, ILogger<MsSqlServiceRegistry> logger)
    {
        _configuration = configuration;
        _client = client;
        _logger = logger;

        _instanceId = Guid.NewGuid().ToString();
        _serviceName = configuration.ServiceName;
        _serviceAddress = configuration.ServiceAddress;
    }

    public async Task RegisterServiceAsync(CancellationToken token)
    {
        // Create a lease that expires after 30 seconds
        _leaseId = await _client.LeaseGrantAsync(token);

        var serviceInstanceInfo = new
        {
            Id = _instanceId,
            Name = _serviceName,
            Address = AddressResolver.Resolve(_serviceAddress),
        };

        var instanceKey = $"/services/{_serviceName}/{_instanceId}";
        var instanceValue = JsonSerializer.Serialize(serviceInstanceInfo);

        // Register instance
        await _client.AddKeyValueAsync(instanceKey, instanceValue, _leaseId, token);

        _logger.LogInformation("Successfully registered service {@ServiceInstanceInfo} with lease {LeaseId}", serviceInstanceInfo, _leaseId);
    }

    public async Task<bool> IsLeaderAsync(CancellationToken token)
    {
        var isLeader = false;

        try
        {
            var instances = await GetRegisteredServicesAsync(token);

            var firstInstance = instances.FirstOrDefault();

            isLeader = _instanceId.Equals(firstInstance?.Id);

        }
        catch (Exception)
        {
            isLeader = false;
        }

        return isLeader;
    }

    public async Task SendHeartbeatsAsync(CancellationToken token)
    {
        _logger.LogInformation("Sending Heartbeats");

        while (!token.IsCancellationRequested)
        {
            await _client.LeaseKeepAliveAsync(_leaseId, token);

            _logger.LogInformation("Lease {LeaseId} kept alive", _leaseId);

            var delay = new TimeSpan(_configuration.TimeToLiveSeconds.Ticks / 2);

            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    public async Task<IList<ServiceInstance>> GetRegisteredServicesAsync(CancellationToken token)
    {
        var response = await _client.GetRangeAsync($"/services/{_serviceName}", token).ConfigureAwait(false);

        var result = new List<ServiceInstance>();

        foreach (var kv in response)
        {
            var instance = JsonSerializer.Deserialize<ServiceInstance>(kv.Value);
            if (instance == null) continue;

            instance.IsThisInstance = _instanceId == instance.Id;

            result.Add(instance);
        }

        return result;
    }

    public async Task UnregisterServiceAsync(CancellationToken token)
    {
        await _client.LeaseRevokeAsync(_leaseId, token);
    }
}