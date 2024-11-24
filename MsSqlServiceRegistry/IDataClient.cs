namespace MsSqlServiceRegistry;

public interface IDataClient
{
    Task<long> LeaseGrantAsync(CancellationToken token);
    Task LeaseKeepAliveAsync(long leaseId, CancellationToken token);
    Task LeaseRevokeAsync(long leaseId, CancellationToken token);
    Task AddKeyValueAsync(string key, string value, long? leaseId, CancellationToken token);
    Task<KeyValue[]> GetRangeAsync(string keyPrefix, CancellationToken token);
    Task RemoveKeyValueAsync(string key, CancellationToken token);
}