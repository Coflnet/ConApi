using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Events for timeline functionality
/// </summary>
public class Event : BaseEntity
{
    /// <summary>
    /// Type of event
    /// </summary>
    public EventType Type { get; set; }
    
    /// <summary>
    /// Title/name of the event
    /// </summary>
    [PartitionKey(1)]
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    [ClusteringKey(0)]
    public DateTime EventDate { get; set; }
    
    /// <summary>
    /// End date for events with duration
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Description of the event
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Type of entity this event is about
    /// </summary>
    public EntityType TargetEntityType { get; set; }
    
    /// <summary>
    /// ID of the entity this event is about (person, place, or thing)
    /// </summary>
    [ClusteringKey(1)]
    public Guid TargetEntityId { get; set; }
    
    /// <summary>
    /// Place where the event occurred
    /// </summary>
    public Guid? PlaceId { get; set; }

    /// <summary>
    /// Flexible attributes stored as a map of key -> value
    /// </summary>
    public IDictionary<string,string>? Attributes { get; set; }
}

/// <summary>
/// Denormalized table for querying events by target entity
/// </summary>
public class EventByTarget
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public Guid TargetEntityId { get; set; }

    [ClusteringKey(1)]
    public DateTime EventDate { get; set; }

    [ClusteringKey(2)]
    public Guid EventId { get; set; }

    public string Title { get; set; } = string.Empty;
    public EventType Type { get; set; }
    public string? Description { get; set; }
    public Guid? PlaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Types of events
/// </summary>
public enum EventType
{
    Unknown = 0,
    Birth = 1,
    Death = 2,
    Marriage = 3,
    Divorce = 4,
    Move = 5,
    Education = 6,
    Employment = 7,
    Purchase = 8,
    Sale = 9,
    Accident = 10,
    Achievement = 11,
    Meeting = 12,
    Trip = 13,
    Memory = 14,
    Other = 99
}

/// <summary>
/// Flexible attributes for events (like PersonData)
/// </summary>
public class EventData
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [PartitionKey]
    public Guid EventId { get; set; }
    
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
