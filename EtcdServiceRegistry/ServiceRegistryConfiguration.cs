﻿namespace EtcdServiceRegistry;

public class ServiceRegistryConfiguration
{
    public string ConnectionString { get; set; } = "";

    public string ServiceName { get; set; } = "";

    public string ServiceAddress { get; set; } = "";
    
    public TimeSpan TimeToLiveSeconds { get; set; } = TimeSpan.FromSeconds(30);
}