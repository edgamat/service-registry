using System.Data;
using Microsoft.Data.SqlClient;

namespace MsSqlServiceRegistry;

public class DataClient : IDataClient
{
    private readonly MsSqlServiceRegistryConfiguration _configuration;

    public DataClient(MsSqlServiceRegistryConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<long> LeaseGrantAsync(CancellationToken token)
    {
        const string sql = @"
SET NOCOUNT ON;
INSERT INTO [dbo].[Leases]
   ([TimeToLiveSeconds]
   ,[ExpiresAt]
   ,[LastRenewedAt])
OUTPUT INSERTED.[LeaseId]
VALUES
   (@TimeToLiveSeconds
   ,dateadd(s, @TimeToLiveSeconds, getutcdate())
   ,getutcdate())
";

        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@TimeToLiveSeconds", _configuration.TimeToLiveSeconds.TotalSeconds);

        await conn.OpenAsync(token).ConfigureAwait(false);
        var result = await cmd.ExecuteScalarAsync(token).ConfigureAwait(false);

        return (result is null) ? 0 : (long)result;
    }

    public async Task LeaseKeepAliveAsync(long leaseId, CancellationToken token)
    {
        const string sql = @"
SET NOCOUNT ON;
UPDATE [dbo].[Leases]
SET 
    [LastRenewedAt] = getutcdate(), 
    [ExpiresAt] = dateadd(s, [TimeToLiveSeconds], getutcdate()) 
WHERE [LeaseId] = @Id
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

    public async Task LeaseRevokeAsync(long leaseId, CancellationToken token)
    {
        const string sql = @"
SET NOCOUNT ON;
DELETE FROM [dbo].[Leases]
WHERE [LeaseId] = @Id
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

    public async Task AddKeyValueAsync(string key, string value, long? leaseId, CancellationToken token)
    {
        var id = Guid.NewGuid();

        const string sql = @"
INSERT INTO [dbo].[KeyValues]
           ([KeyValueKey]
           ,[KeyValueValue]
           ,[Revision]
           ,[CreatedAt]
           ,[UpdatedAt]
           ,[LeaseId])
     VALUES
           (@Key
           ,@Value
           ,@Revision
           ,getutcdate()
           ,getutcdate()
           ,@LeaseId)
";

        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.Parameters.AddWithValue("@Revision", 1);
        cmd.Parameters.AddWithValue("@LeaseId", leaseId);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    public async Task<KeyValue[]> GetRangeAsync(string keyPrefix, CancellationToken token)
    {
        const string sql = @"
SET NOCOUNT ON;
SELECT r.KeyValueKey, r.KeyValueValue, r.Revision, r.CreatedAt, r.UpdatedAt, r.LeaseId 
FROM [dbo].[KeyValues] r
LEFT JOIN [dbo].[Leases] l on l.LeaseId = r.LeaseId
WHERE KeyValueKey LIKE @InstanceKeyPrefix
AND (COALESCE(l.[ExpiresAt], getutcdate()) >= getutcdate()) 
ORDER BY r.CreatedAt
";
        
        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@InstanceKeyPrefix", $"{keyPrefix}%");

        var instances = new List<KeyValue>();

        await conn.OpenAsync(token).ConfigureAwait(false);
        await using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);

        while (await reader.ReadAsync(token))
        {
            var createdAt = reader.GetDateTime(3);
            var updatedAt = reader.GetDateTime(4);
            
            var instance = new KeyValue
            {
                Key = reader.GetString(0),
                Value = reader.GetString(1),
                Revision = reader.GetInt32(2),
                CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc),
                LeaseId = reader.IsDBNull(5) ? null : reader.GetInt64(5)
            };
            
            instances.Add(instance);
        }

        return instances.ToArray();
    }

    public async Task RemoveKeyValueAsync(string key, CancellationToken token)
    {
        const string sql = @"
SET NOCOUNT ON;
DELETE FROM [dbo].[KeyValues]
WHERE [KeyValueKey] = @Key
";

        await using var conn = new SqlConnection(_configuration.ConnectionString);
        await using var cmd = new SqlCommand();

        cmd.Connection = conn;
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("@Key", key);

        await conn.OpenAsync(token).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}