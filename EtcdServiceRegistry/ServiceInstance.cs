namespace EtcdServiceRegistry;

public class ServiceInstance
{
    public required string Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string Address { get; set; }
    
    public bool IsThisInstance { get; set; }
}