using System.Text.Json;
using System.Text.Json.Serialization;
using dotnet_etcd.interfaces;
using Etcdserverpb;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace EtcdServiceRegistry;

public class ServiceRegistry
{
    private readonly IEtcdClient _client;
    private readonly ILogger<ServiceRegistry> _logger;
    private readonly string _instanceId;
    private readonly string _serviceName;
    private readonly string _serviceInstanceKey;
    private readonly string _serviceAddress;
    private long _leaseId;
    
    public ServiceRegistry(
        ServiceRegistryConfiguration configuration, 
        IEtcdClient client,
        ILogger<ServiceRegistry> logger)
    {
        _client = client;
        _logger = logger;
        _instanceId = Guid.NewGuid().ToString();
        _serviceName = configuration.ServiceName;
        _serviceAddress = configuration.ServiceAddress;
        _serviceInstanceKey = $"/services/{_serviceName}/{_instanceId}";
    }
    public async Task RegisterServiceAsync(CancellationToken token)
    {
        // Create a lease that expires after 30 seconds 
        var leaseGrantRequest = new LeaseGrantRequest { TTL = 30 };
        var leaseGrantResponse = await _client.LeaseGrantAsync(leaseGrantRequest, null, null, token).ConfigureAwait(false);
        _leaseId = leaseGrantResponse.ID;
        
        var serviceInstanceInfo = new 
        {
            Id = _instanceId,
            Name = _serviceName,
            Address = AddressResolver.Resolve(_serviceAddress),
        };

        var serviceValue = JsonSerializer.Serialize(serviceInstanceInfo);

        var putRequest = new PutRequest
        {
            Key = ByteString.CopyFromUtf8(_serviceInstanceKey),
            Value = ByteString.CopyFromUtf8(serviceValue),
            Lease = _leaseId
        };

        await _client.PutAsync(putRequest, null, null, token).ConfigureAwait(false);        
        
        _logger.LogInformation("Successfully registered service {ServiceInstanceInfo} with lease {LeaseId}", serviceInstanceInfo, _leaseId);
    }

    public async Task SendHeartbeatsAsync(CancellationToken token)
    {
        await _client.LeaseKeepAlive(_leaseId, token).ConfigureAwait(false);
    }

    public async Task<bool> UnregisterServiceAsync(CancellationToken token)
    {
        try
        {
            await _client.DeleteAsync(_serviceInstanceKey, null, null, token).ConfigureAwait(false);
        
            _logger.LogInformation("Successfully un-registered service {ServiceInstanceKey}", _serviceInstanceKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to un-registered service {ServiceInstanceKey}", _serviceInstanceKey);
            
            return false;
        }
    }
    
    public async Task<ServiceInstance[]> GetRegisteredServicesAsync(CancellationToken token)
    {
        var response = await _client.GetRangeAsync($"/services/{_serviceName}", null, null, token).ConfigureAwait(false);

        var result = new List<ServiceInstance>();

        foreach (var kv in response.Kvs)
        {
            var instance = JsonSerializer.Deserialize<ServiceInstance>(kv.Value.ToStringUtf8());
            if (instance == null) continue;
            
            instance.IsThisInstance = _instanceId == instance.Id;

            result.Add(instance);
        }

        return result.ToArray();
    }
}
