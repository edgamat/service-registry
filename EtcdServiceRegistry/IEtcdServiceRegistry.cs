namespace EtcdServiceRegistry;

public interface IEtcdServiceRegistry
{
    Task RegisterServiceAsync(CancellationToken token);

    Task UnregisterServiceAsync(CancellationToken token);
}