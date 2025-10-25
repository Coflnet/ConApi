using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Document/file metadata
/// </summary>
public class Document : BaseEntity
{
    /// <summary>
    /// Original filename
    /// </summary>
    [PartitionKey(1)]
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Display title
    /// </summary>
    public string? Title { get; set; }
    
    /// <summary>
    /// Document description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// MIME type
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// S3/R2 object key
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;
    
    /// <summary>
    /// S3/R2 bucket name
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Document category/type
    /// </summary>
    public DocumentType Type { get; set; }
    
    /// <summary>
    /// Optional date associated with document (photo date, document date, etc.)
    /// </summary>
    public DateTime? DocumentDate { get; set; }
    
    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string>? Tags { get; set; }
    
    /// <summary>
    /// MD5 hash for deduplication
    /// </summary>
    public string? ContentHash { get; set; }
}

/// <summary>
/// Link between documents and entities
/// </summary>
public class DocumentLink
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [ClusteringKey(0)]
    public Guid EntityId { get; set; }
    
    [ClusteringKey(1)]
    public Guid DocumentId { get; set; }
    
    /// <summary>
    /// Type of entity
    /// </summary>
    public EntityType EntityType { get; set; }
    
    /// <summary>
    /// Caption or note about this link
    /// </summary>
    public string? Caption { get; set; }
    
    /// <summary>
    /// Display order
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// When the link was created
    /// </summary>
    public DateTime LinkedAt { get; set; }
}

/// <summary>
/// Denormalized table for querying documents by entity
/// </summary>
public class DocumentByEntity
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    [ClusteringKey(0)]
    public Guid EntityId { get; set; }
    
    [ClusteringKey(1)]
    public int DisplayOrder { get; set; }
    
    [ClusteringKey(2)]
    public Guid DocumentId { get; set; }
    
    public string FileName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Storage quota tracking per user
/// </summary>
public class StorageQuota
{
    [PartitionKey]
    public Guid UserId { get; set; }
    
    /// <summary>
    /// Total bytes used
    /// </summary>
    public long UsedBytes { get; set; }
    
    /// <summary>
    /// Quota limit in bytes
    /// </summary>
    public long QuotaBytes { get; set; }
    
    /// <summary>
    /// Number of documents
    /// </summary>
    public int DocumentCount { get; set; }
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Types of documents
/// </summary>
public enum DocumentType
{
    Unknown = 0,
    Photo = 1,
    Document = 2,
    Certificate = 3,
    Letter = 4,
    Video = 5,
    Audio = 6,
    Other = 99
}
