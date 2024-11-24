namespace ServiceRegistry.Abstractions;

public interface IServiceRegistry
{
    Task RegisterServiceAsync(CancellationToken token);

    Task<bool> IsLeaderAsync(CancellationToken token);

    Task SendHeartbeatsAsync(CancellationToken token);

    Task<IList<ServiceInstance>> GetRegisteredServicesAsync(CancellationToken token);
    
    Task UnregisterServiceAsync(CancellationToken token);
}