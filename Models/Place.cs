using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Place entity with hierarchical support
/// </summary>
public class Place : BaseEntity
{
    /// <summary>
    /// Name of the place
    /// </summary>
    [PartitionKey(1)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of place (city, village, country, etc.)
    /// </summary>
    public PlaceType Type { get; set; }
    
    /// <summary>
    /// Parent place ID for hierarchy (e.g., village -> county -> country)
    /// </summary>
    public Guid? ParentPlaceId { get; set; }
    
    /// <summary>
    /// Latitude coordinate
    /// </summary>
    public double? Latitude { get; set; }
    
    /// <summary>
    /// Longitude coordinate
    /// </summary>
    public double? Longitude { get; set; }
    
    /// <summary>
    /// Full hierarchical path (e.g., "Germany/Bavaria/Munich")
    /// </summary>
    public string? HierarchyPath { get; set; }

    /// <summary>
    /// Flexible attributes stored as a map of key -> value
    /// </summary>
    public IDictionary<string,string>? Attributes { get; set; }
}

/// <summary>
/// Types of places
/// </summary>
public enum PlaceType
{
    Unknown = 0,
    Continent = 1,
    Country = 2,
    State = 3,
    County = 4,
    City = 5,
    Village = 6,
    District = 7,
    Building = 8,
    Other = 99
}

/// <summary>
/// Flexible attributes for places (like PersonData)
/// </summary>
public class PlaceData
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [PartitionKey]
    public Guid PlaceId { get; set; }
    
    /// <summary>
    /// Category of this datapoint
    /// </summary>
    [ClusteringKey(0)]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// Field name
    /// </summary>
    [ClusteringKey(1)]
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// The content of this datapoint
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// When this datapoint was last updated
    /// </summary>
    public DateTime ChangedAt { get; set; }
}
