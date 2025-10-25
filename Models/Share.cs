using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Sharing invitation for data sharing between users
/// </summary>
public class ShareInvitation : BaseEntity
{
    /// <summary>
    /// User who sent the invitation
    /// </summary>
    [PartitionKey(1)]
    public Guid FromUserId { get; set; }
    
    /// <summary>
    /// User who receives the invitation
    /// </summary>
    [ClusteringKey(0)]
    public Guid ToUserId { get; set; }
    
    /// <summary>
    /// Type of entity being shared
    /// </summary>
    public EntityType EntityType { get; set; }
    
    /// <summary>
    /// ID of the entity being shared
    /// </summary>
    public Guid EntityId { get; set; }
    
    /// <summary>
    /// Status of the invitation
    /// </summary>
    public ShareStatus Status { get; set; }
    
    /// <summary>
    /// Permission level granted
    /// </summary>
    public SharePermission Permission { get; set; }
    
    /// <summary>
    /// Optional message from sender
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// When the invitation expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// When the invitation was accepted
    /// </summary>
    public DateTime? AcceptedAt { get; set; }
}

/// <summary>
/// Denormalized table for querying invitations by recipient
/// </summary>
public class ShareInvitationByRecipient
{
    [PartitionKey]
    public Guid ToUserId { get; set; }
    
    [ClusteringKey(0)]
    public ShareStatus Status { get; set; }
    
    [ClusteringKey(1)]
    public Guid InvitationId { get; set; }
    
    public Guid FromUserId { get; set; }
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public SharePermission Permission { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Status of a share invitation
/// </summary>
public enum ShareStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Revoked = 3,
    Expired = 4
}

/// <summary>
/// Permission level for shared data
/// </summary>
public enum SharePermission
{
    View = 0,
    Edit = 1,
    Admin = 2
}

/// <summary>
/// Tracks data provenance - who created/modified what
/// </summary>
public class DataProvenance
{
    [PartitionKey]
    public Guid EntityId { get; set; }
    
    [ClusteringKey(0)]
    public DateTime ChangedAt { get; set; }
    
    /// <summary>
    /// User who made the change
    /// </summary>
    public Guid ChangedBy { get; set; }
    
    /// <summary>
    /// Type of change
    /// </summary>
    public ChangeType ChangeType { get; set; }
    
    /// <summary>
    /// Field that was changed
    /// </summary>
    public string? FieldName { get; set; }
    
    /// <summary>
    /// Old value (JSON)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value (JSON)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// Source of the change (manual, import, merge, etc.)
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Type of data change
/// </summary>
public enum ChangeType
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    Merged = 3,
    Imported = 4
}

/// <summary>
/// Conflict when merging shared data
/// </summary>
public class DataConflict
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [ClusteringKey(0)]
    public Guid EntityId { get; set; }
    
    [ClusteringKey(1)]
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// Current value in user's data
    /// </summary>
    public string? LocalValue { get; set; }
    
    /// <summary>
    /// Incoming value from shared source
    /// </summary>
    public string? RemoteValue { get; set; }
    
    /// <summary>
    /// User who owns the remote data
    /// </summary>
    public Guid RemoteUserId { get; set; }
    
    /// <summary>
    /// Resolution strategy
    /// </summary>
    public ConflictResolution? Resolution { get; set; }
    
    /// <summary>
    /// When the conflict was detected
    /// </summary>
    public DateTime DetectedAt { get; set; }
    
    /// <summary>
    /// When the conflict was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Conflict resolution strategy
/// </summary>
public enum ConflictResolution
{
    KeepLocal = 0,
    KeepRemote = 1,
    KeepBoth = 2,
    Merge = 3,
    Manual = 4
}
