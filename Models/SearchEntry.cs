using Cassandra.Mapping.Attributes;

namespace Coflnet.Connections;

public class SearchEntry
{
    [PartitionKey]
    public string UserId { get; set; }
    public string KeyWord { get; set; }
    public ResultType Type { get; set; }
    public string FullId { get; set; }
    public string Text { get; set; }

    public enum ResultType
    {
        Unknown,
        Person,
        Location
    }
}