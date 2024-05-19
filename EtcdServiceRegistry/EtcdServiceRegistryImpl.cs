using System.Text.Json;
using dotnet_etcd;
using Microsoft.Extensions.Logging;

namespace EtcdServiceRegistry;

public class EtcdServiceRegistryImpl : IEtcdServiceRegistry
{
    private readonly ILogger<EtcdServiceRegistryImpl> _logger;
    private readonly EtcdClient _client;
    private readonly string _serviceId;
    private readonly string _serviceName;
    private readonly string _serviceKey;
    private readonly string _serviceAddress;
    
    public EtcdServiceRegistryImpl(ServiceRegistryConfiguration configuration, ILogger<EtcdServiceRegistryImpl> logger)
    {
        _logger = logger;
        _serviceId = Guid.NewGuid().ToString();
        _serviceName = configuration.ServiceName;
        _serviceKey = $"/services/{_serviceName}/{_serviceId}";
        _serviceAddress = AddressResolver.Resolve(configuration.ServiceAddress);
        
        _client = new EtcdClient(configuration.ConnectionString);
    }

    public async Task RegisterServiceAsync(CancellationToken token)
    {
        var serviceInfo = new
        {
            Id = _serviceId,
            Name = _serviceName,
            Address = _serviceAddress,
        };

        var serviceValue = JsonSerializer.Serialize(serviceInfo);

        await _client.PutAsync(_serviceKey, serviceValue, null, null, token);
        
        _logger.LogInformation("Successfully registered service {ServiceInfo}", serviceInfo);
    }

    public async Task UnregisterServiceAsync(CancellationToken token)
    {
        await _client.DeleteAsync(_serviceKey, null, null, token);
        
        _logger.LogInformation("Successfully un-registered service {ServiceKey}", _serviceKey);
    }
}