using Cassandra.Mapping.Attributes;

namespace Coflnet.Connections;

public class PersonData
{
    [PartitionKey]
    public string UserId { get; set; }
    [ClusteringKey(1)]
    public string Name { get; set; }
    [ClusteringKey(2)]
    public DateTime Birthday { get; set; }
    [ClusteringKey(3)]
    public string BirthPlace { get; set; }
    [ClusteringKey(4)]
    public string Category { get; set; }
    [ClusteringKey(5)]
    public string Key { get; set; }
    public string Value { get; set; }
}
