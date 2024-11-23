using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public class MsSqlServiceRegistry : IServiceRegistry
{
    private readonly MsSqlServiceRegistryConfiguration _configuration;
    private readonly ILogger<MsSqlServiceRegistry> _logger;

    private readonly string _instanceId;
    private readonly string _serviceName;
    private readonly string _serviceInstanceKey;
    private readonly string _serviceAddress;
    private readonly string _serviceLeaderKey;
    private Guid _leaseId;
    
    public MsSqlServiceRegistry(MsSqlServiceRegistryConfiguration configuration, ILogger<MsSqlServiceRegistry> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _instanceId = Guid.NewGuid().ToString();
        _serviceName = configuration.ServiceName;
        _serviceAddress = configuration.ServiceAddress;
        _serviceInstanceKey = $"/services/{_serviceName}/{_instanceId}";
        _serviceLeaderKey = $"/leaders/{_serviceName}";
    }
    
    public async Task RegisterServiceAsync(CancellationToken token)
    {
        // Create a lease that expires after 30 seconds
        _leaseId = await LeaseGrantAsync(token); 

        var serviceInstanceInfo = new 
        {
            Id = _instanceId,
            Name = _serviceName,
            Address = AddressResolver.Resolve(_serviceAddress),
        };

        // Register instance
        await RegisterInstanceAsync(token);
        
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
            await LeaseKeepAliveAsync(token);
            
            _logger.LogInformation("Lease {LeaseId} kept alive", _leaseId);

            var delay = new TimeSpan(_configuration.TimeToLiveSeconds.Ticks / 2);
            
            await Task.Delay(delay, token).ConfigureAwait(false);
        }
    }

    public async Task<ServiceInstance[]> GetRegisteredServicesAsync(CancellationToken token)
    {
        const string sql = @"
SELECT r.Id, r.InstanceKey, r.ServiceName, r.HostAddress, r.LeaseId, l.LastRenewedAt 
FROM [dbo].[Registrations] r
JOIN [dbo].[Leases] l on l.Id = r.LeaseId
WHERE InstanceKey LIKE @InstanceKeyPrefix
AND l.EvictAt >= getutcdate() 
ORDER BY l.RegisteredAt
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@InstanceKeyPrefix", $"/services/{_serviceName}%");

        var instances = new List<ServiceInstance>();

        await conn.OpenAsync(token).ConfigureAwait(false);
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);

        while (await reader.ReadAsync(token))
        {
            var instanceKey = reader.GetString(1);
            var instance = new ServiceInstance
            {
                Id = reader.GetGuid(0).ToString(),
                Address = reader.GetString(3),
                Name = reader.GetString(2),
                IsThisInstance = _serviceInstanceKey == instanceKey
            };
            
            instances.Add(instance);
        }

        return instances.ToArray();
    }

    public async Task UnregisterServiceAsync(CancellationToken token)
    {
        const string sql = @"
DELETE FROM [dbo].[Leases]
WHERE Id = @Id
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Id", _leaseId);
        
        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task<Guid> LeaseGrantAsync(CancellationToken token)
    {
        var id = Guid.NewGuid();
        
        const string sql = @"
INSERT INTO [dbo].[Leases]
           ([Id]
           ,[RenewalInterval]
           ,[Duration]
           ,[RegisteredAt]
           ,[LastRenewedAt]
           ,[EvictAt]
           ,[AliveSince])
     VALUES
           (@Id
           ,@RenewalInterval
           ,@Duration
           ,getutcdate()
           ,getutcdate()
           ,dateadd(s, @Duration, getutcdate())
           ,getutcdate())
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@RenewalInterval", _configuration.TimeToLiveSeconds.TotalSeconds);
        cmd.Parameters.AddWithValue("@Duration", _configuration.TimeToLiveSeconds.TotalSeconds);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

        return id;
    }

    private async Task RegisterInstanceAsync(CancellationToken token)
    {
        var id = Guid.NewGuid();
        
        const string sql = @"
INSERT INTO [dbo].[Registrations]
           ([Id]
           ,[InstanceKey]
           ,[ServiceName]
           ,[HostAddress]
           ,[LeaseId])
     VALUES
           (@Id
           ,@InstanceKey
           ,@ServiceName
           ,@HostAddress
           ,@LeaseId)
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@InstanceKey", _serviceInstanceKey);
        cmd.Parameters.AddWithValue("@ServiceName", _serviceName);
        cmd.Parameters.AddWithValue("@HostAddress", AddressResolver.Resolve(_serviceAddress));
        cmd.Parameters.AddWithValue("@LeaseId", _leaseId);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private async Task LeaseKeepAliveAsync(CancellationToken token)
    {
        const string sql = @"
UPDATE [dbo].[Leases]
SET 
    LastRenewedAt = getutcdate(), 
    EvictAt = dateadd(s, Duration, getutcdate()) 
WHERE Id = @Id
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Id", _leaseId);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}