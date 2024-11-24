namespace MsSqlServiceRegistry;

public class KeyValue
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public int Revision { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? LeaseId { get; set; }
}