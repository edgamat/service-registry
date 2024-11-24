using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public interface IDataContext
{
    Task RegisterInstanceAsync(string serviceName, string serviceAddress, string instanceId, Guid leaseId, CancellationToken token);
    Task<ServiceInstance[]> GetServiceInstancesAsync(string serviceName, string instanceId, CancellationToken token);
    Task<Guid> LeaseGrantAsync(CancellationToken token);
    Task LeaseKeepAliveAsync(Guid leaseId, CancellationToken token);
    Task LeaseRevokeAsync(Guid leaseId, CancellationToken token);
}