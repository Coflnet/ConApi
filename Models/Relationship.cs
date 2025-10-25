using Cassandra.Mapping.Attributes;

namespace Coflnet.Connections;

/// <summary>
/// Represents a relationship between two entities (bidirectional)
/// </summary>
public class Relationship
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Unique ID for this relationship
    /// </summary>
    [PartitionKey]
    public Guid Id { get; set; }
    
    /// <summary>
    /// Source entity type
    /// </summary>
    public Models.EntityType FromEntityType { get; set; }
    
    /// <summary>
    /// Source entity ID (string to allow composite IDs like Name;YYYY-MM-DD;Place)
    /// </summary>
    [ClusteringKey(0)]
    public Guid FromEntityId { get; set; }
    
    /// <summary>
    /// Target entity type
    /// </summary>
    public Models.EntityType ToEntityType { get; set; }
    
    /// <summary>
    /// Target entity ID (string to allow composite IDs)
    /// </summary>
    [ClusteringKey(1)]
    public Guid ToEntityId { get; set; }
    
    /// <summary>
    /// Type of relationship (e.g., "parent", "spouse", "owner", "Mutter", "Vater")
    /// </summary>
    public string RelationshipType { get; set; } = string.Empty;
    
    /// <summary>
    /// Language of the relationship type (de, en, etc.)
    /// </summary>
    public string Language { get; set; } = "de";
    
    /// <summary>
    /// When the relationship started
    /// </summary>
    public DateTime? StartDate { get; set; }
    
    /// <summary>
    /// When the relationship ended
    /// </summary>
    public DateTime? EndDate { get; set; }
    
    /// <summary>
    /// Certainty level (0-100%)
    /// </summary>
    public int Certainty { get; set; } = 100;
    
    /// <summary>
    /// Source of this information
    /// </summary>
    public string? Source { get; set; }
    
    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }
    
    /// <summary>
    /// When this relationship was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When this relationship was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Is this the primary direction (for bidirectional relationships)
    /// The inverse relationship will have this set to false
    /// </summary>
    public bool IsPrimary { get; set; } = true;
}

/// <summary>
/// Denormalized table to query relationships by source entity (optimized for reads)
/// </summary>
public class RelationshipByFrom
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public Guid FromEntityId { get; set; }

    [ClusteringKey(1)]
    public Guid ToEntityId { get; set; }

    public Guid Id { get; set; }
    public Models.EntityType FromEntityType { get; set; }
    public Models.EntityType ToEntityType { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Language { get; set; } = "de";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Certainty { get; set; } = 100;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPrimary { get; set; } = true;
}

/// <summary>
/// Denormalized table to query relationships by target entity (if needed)
/// </summary>
public class RelationshipByTo
{
    [PartitionKey]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public Guid ToEntityId { get; set; }

    [ClusteringKey(1)]
    public Guid FromEntityId { get; set; }

    public Guid Id { get; set; }
    public Models.EntityType FromEntityType { get; set; }
    public Models.EntityType ToEntityType { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Language { get; set; } = "de";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Certainty { get; set; } = 100;
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPrimary { get; set; } = true;
}

/// <summary>
/// Predefined relationship types with translations
/// </summary>
public class RelationshipType
{
    [PartitionKey]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Language code (de, en)
    /// </summary>
    [ClusteringKey(0)]
    public string Language { get; set; } = string.Empty;
    
    /// <summary>
    /// Translated name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Inverse relationship type (e.g., parent/child)
    /// </summary>
    public string? InverseType { get; set; }
    
    /// <summary>
    /// Category (family, ownership, etc.)
    /// </summary>
    public string Category { get; set; } = string.Empty;
}
