namespace EtcdServiceRegistry;

public class ServiceRegistryConfiguration
{
    public required string ConnectionString { get; set; }
    
    public required string ServiceName { get; set; }
    
    public required string ServiceAddress { get; set; }
    
    public TimeSpan TimeToLiveSeconds { get; set; } = TimeSpan.FromSeconds(30);
}