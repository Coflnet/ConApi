using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Physical things/objects (cars, toys, buildings, etc.)
/// </summary>
public class Thing : BaseEntity
{
    /// <summary>
    /// Name/identifier of the thing
    /// </summary>
    [PartitionKey(1)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of thing
    /// </summary>
    public ThingType Type { get; set; }
    
    /// <summary>
    /// Current owner (person ID)
    /// </summary>
    // OwnerId is a Guid referencing the owner's internal ID
    public Guid? OwnerId { get; set; }
    
    /// <summary>
    /// Manufacturer or creator
    /// </summary>
    public string? Manufacturer { get; set; }
    
    /// <summary>
    /// Year of manufacture/creation
    /// </summary>
    public int? YearMade { get; set; }
    
    /// <summary>
    /// Model or type designation
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Serial number or registration
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Flexible attributes stored as a map of key -> value
    /// </summary>
    public IDictionary<string,string>? Attributes { get; set; }
}

/// <summary>
/// Denormalized table for querying things by owner
/// </summary>
public class ThingByOwner
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public Guid OwnerId { get; set; }

    [ClusteringKey(1)]
    public Guid ThingId { get; set; }

    public string Name { get; set; } = string.Empty;
    public ThingType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Denormalized table to support lookups by (user_id, name)
/// </summary>
public class ThingByName
{
    [PartitionKey(0)]
    public Guid UserId { get; set; }

    [PartitionKey(1)]
    public string Name { get; set; } = string.Empty;

    [ClusteringKey(0)]
    public Guid ThingId { get; set; }

    public ThingType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Types of things
/// </summary>
public enum ThingType
{
    Unknown = 0,
    Vehicle = 1,
    Building = 2,
    Toy = 3,
    Tool = 4,
    Furniture = 5,
    Jewelry = 6,
    Document = 7,
    Book = 8,
    Artwork = 9,
    Clothing = 10,
    Other = 99
}

/// <summary>
/// Flexible attributes for things (like PersonData)
/// </summary>
public class ThingData
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [PartitionKey]
    public Guid ThingId { get; set; }
    
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
