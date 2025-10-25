namespace Coflnet.Connections.Models;

/// <summary>
/// Base class for all entities in the system
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Unique identifier for this entity
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// User who owns this entity
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// When this entity was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When this entity was last modified
    /// </summary>
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// Privacy level of this entity
    /// </summary>
    public PrivacyLevel PrivacyLevel { get; set; }
}

/// <summary>
/// Privacy levels for entities
/// </summary>
public enum PrivacyLevel
{
    /// <summary>
    /// Only visible to the owner
    /// </summary>
    Private = 0,
    
    /// <summary>
    /// Visible to family members (shared with)
    /// </summary>
    Family = 1,
    
    /// <summary>
    /// Visible to friends (shared with)
    /// </summary>
    Friends = 2,
    
    /// <summary>
    /// Publicly visible
    /// </summary>
    Public = 3
}

/// <summary>
/// Types of entities in the system
/// </summary>
public enum EntityType
{
    Unknown = 0,
    Person = 1,
    Place = 2,
    Thing = 3,
    Event = 4
}
