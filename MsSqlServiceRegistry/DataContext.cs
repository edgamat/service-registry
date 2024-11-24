using System.Data;
using Microsoft.Data.SqlClient;
using ServiceRegistry.Abstractions;

namespace MsSqlServiceRegistry;

public class DataContext : IDataContext
{
    private readonly MsSqlServiceRegistryConfiguration _configuration;

    public DataContext(MsSqlServiceRegistryConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task RegisterInstanceAsync(string serviceName, string serviceAddress, string instanceId, Guid leaseId, CancellationToken token)
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
        cmd.Parameters.AddWithValue("@InstanceKey", $"/services/{serviceName}/{instanceId}");
        cmd.Parameters.AddWithValue("@ServiceName", serviceName);
        cmd.Parameters.AddWithValue("@HostAddress", AddressResolver.Resolve(serviceAddress));
        cmd.Parameters.AddWithValue("@LeaseId", leaseId);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    
    public async Task<ServiceInstance[]> GetServiceInstancesAsync(string serviceName, string instanceId, CancellationToken token)
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

        cmd.Parameters.AddWithValue("@InstanceKeyPrefix", $"/services/{serviceName}%");

        var instances = new List<ServiceInstance>();

        await conn.OpenAsync(token).ConfigureAwait(false);
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);

        var thisInstance = $"/services/{serviceName}/{instanceId}";
        
        while (await reader.ReadAsync(token))
        {
            var instanceKey = reader.GetString(1);
            var instance = new ServiceInstance
            {
                Id = reader.GetGuid(0).ToString(),
                Address = reader.GetString(3),
                Name = reader.GetString(2),
                IsThisInstance = thisInstance == instanceKey
            };
            
            instances.Add(instance);
        }

        return instances.ToArray();
    }
    
    public async Task<Guid> LeaseGrantAsync(CancellationToken token)
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

    public async Task LeaseKeepAliveAsync(Guid leaseId, CancellationToken token)
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

        cmd.Parameters.AddWithValue("@Id", leaseId);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    
    public async Task LeaseRevokeAsync(Guid leaseId, CancellationToken token)
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

        cmd.Parameters.AddWithValue("@Id", leaseId);
        
        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}