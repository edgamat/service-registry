using dotnet_etcd.interfaces;
using Etcdserverpb;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace EtcdServiceRegistry;

public delegate void LeaderElectionHandler(object sender, EventArgs e);
    
public class LeaderElector
{
    private readonly IEtcdClient _client;
    private readonly ILogger<LeaderElector> _logger;
    private readonly string _serviceName;
    private readonly string _serviceLeaderKey;
    private long _leaseId;

    public event LeaderElectionHandler LeaderElection;
    
    public LeaderElector(
        ServiceRegistryConfiguration configuration, 
        IEtcdClient client,
        ILogger<LeaderElector> logger)
    {
        _client = client;
        _logger = logger;
        _serviceName = configuration.ServiceName;
        _serviceLeaderKey = $"/services/{_serviceName}/leader";
    }

    public async Task<bool> TryBecomeLeaderAsync(CancellationToken token)
    {
        // Create a lease that expires after 30 seconds 
        var leaseGrantRequest = new LeaseGrantRequest { TTL = 30 };
        var leaseGrantResponse = await _client.LeaseGrantAsync(leaseGrantRequest, null, null, token).ConfigureAwait(false);
        _leaseId = leaseGrantResponse.ID;
        
        try
        {
            // Attempt to create the election key with this lease.
            // If the key already exists, this call will fail.
            var putRequest = new PutRequest
            {
                Key = ByteString.CopyFromUtf8(_serviceLeaderKey),
                Value = ByteString.CopyFromUtf8(_serviceName),
                Lease = _leaseId
            };

            await _client.PutAsync(putRequest, null, null, token).ConfigureAwait(false);        

            _logger.LogInformation("I am now the leader");
            
            OnLeaderElection(EventArgs.Empty);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Failed to become leader: {Message}", ex.Message);
            return false;
        }
    }
    
    // Method to raise the event
    protected virtual void OnLeaderElection(EventArgs e)
    {
        LeaderElection?.Invoke(this, e);
    }

    public async Task SendHeartbeatsAsync(CancellationToken token)
    {
        await _client.LeaseKeepAlive(_leaseId, token).ConfigureAwait(false);
    }
    
    public async Task WatchLeaderKeyAsync(CancellationToken token)
    {
        await _client.WatchAsync(_serviceLeaderKey, response =>
        {
            if (response.Events.Count > 0 && response.Events[0].Type == Mvccpb.Event.Types.EventType.Delete)
            {
                // Leader key was deleted, attempt to become the leader again
                _logger.LogInformation("Leader key deleted, attempting to become leader...");
                TryBecomeLeaderAsync(token).Wait(token);
            }
        }, null, null, token).ConfigureAwait(false);
    }
}