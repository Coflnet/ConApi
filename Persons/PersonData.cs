using Cassandra.Mapping.Attributes;

namespace Coflnet.Connections;

public class PersonData
{
    [PartitionKey]
    public Guid UserId { get; set; }
    /// <summary>
    /// Name on birth, never changes
    /// </summary>
    [PartitionKey]
    public string Name { get; set; }
    [ClusteringKey(2)]
    public DateTime Birthday { get; set; }
    [ClusteringKey(3)]
    public string BirthPlace { get; set; }
    /// <summary>
    /// Category of this datapoint
    /// </summary>
    [ClusteringKey(4)]
    public string Category { get; set; }
    /// <summary>
    /// Field name
    /// </summary>
    [ClusteringKey(5)]
    public string Key { get; set; }
    /// <summary>
    /// The content of this datapoint
    /// </summary>
    public string Value { get; set; }
    /// <summary>
    /// When this datapoint was last updated
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
