using Etcdserverpb;
using Google.Protobuf;

namespace EtcdServiceRegistry;

public static class ElectLeaderRequest
{
    public static TxnRequest Create(string key, string instanceId, long leaseId)
    {
        // Create a transaction request to conditionally put the key if it does not exist
        return new TxnRequest
        {
            Compare =
            {
                DoesTheKeyExist(key)
            },
            Success =
            {
                MakeThisInstanceTheLeader(key, instanceId, leaseId)
            },
            Failure =
            {
                GetTheElectedLeader(key)
            }
        };
    }

    public static string? GetElectedLeaderInstanceId(this TxnResponse txnResponse)
    {
        var responses = txnResponse.Responses.FirstOrDefault();
        var keyValues = responses?.ResponseRange.Kvs.FirstOrDefault();
        return keyValues?.Value?.ToStringUtf8();
    }

    private static Compare DoesTheKeyExist(string key)
    {
        return new Compare
        {
            Key = ByteString.CopyFromUtf8(key),
            Result = Compare.Types.CompareResult.Equal,
            Target = Compare.Types.CompareTarget.Version,
            Version = 0 // version is 0 if key does not exist
        };
    }

    private static RequestOp MakeThisInstanceTheLeader(string key, string instanceId, long leaseId)
    {
        return new RequestOp
        {
            RequestPut = new PutRequest
            {
                Key = ByteString.CopyFromUtf8(key),
                Value = ByteString.CopyFromUtf8(instanceId),
                Lease = leaseId
            }
        };
    }

    private static RequestOp GetTheElectedLeader(string key)
    {
        return new RequestOp
        {
            RequestRange = new RangeRequest
            {
                Key = ByteString.CopyFromUtf8(key),
            }
        };
    }
}