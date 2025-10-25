using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Source citation for tracking information provenance
/// </summary>
[Table("source_citation")]
public class SourceCitation : BaseEntity
{
    /// <summary>
    /// User who owns this citation (overrides base to add partition key)
    /// </summary>
    [PartitionKey(0)]
    public new Guid UserId { get; set; }

    /// <summary>
    /// Entity this citation refers to
    /// </summary>
    [ClusteringKey(0)]
    public Guid EntityId { get; set; }

    /// <summary>
    /// Type of entity being cited
    /// </summary>
    public EntityType EntityType { get; set; }

    /// <summary>
    /// Field name being cited (e.g., "birthDate", "name")
    /// </summary>
    [ClusteringKey(1)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Type of source
    /// </summary>
    public SourceType SourceType { get; set; }

    /// <summary>
    /// Title of the source
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Author of the source
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Publisher
    /// </summary>
    public string? Publisher { get; set; }

    /// <summary>
    /// Publication date
    /// </summary>
    public DateTime? PublicationDate { get; set; }

    /// <summary>
    /// URL or reference
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Archive or repository name
    /// </summary>
    public string? Repository { get; set; }

    /// <summary>
    /// Call number or reference ID
    /// </summary>
    public string? CallNumber { get; set; }

    /// <summary>
    /// Specific page or location within source
    /// </summary>
    public string? Page { get; set; }

    /// <summary>
    /// Quality/reliability rating (0-100)
    /// </summary>
    public int QualityRating { get; set; } = 50;

    /// <summary>
    /// Notes about this citation
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Transcription or quote from source
    /// </summary>
    public string? Transcription { get; set; }

    /// <summary>
    /// Linked document ID (if digitized)
    /// </summary>
    public Guid? DocumentId { get; set; }
}

/// <summary>
/// Denormalized table for querying citations by source
/// </summary>
[Table("citation_by_source")]
public class CitationBySource
{
    [PartitionKey(0)]
    public Guid UserId { get; set; }

    [ClusteringKey(0)]
    public string Title { get; set; } = string.Empty;

    [ClusteringKey(1)]
    public Guid CitationId { get; set; }

    public Guid EntityId { get; set; }
    public EntityType EntityType { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public int QualityRating { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Type of source document
/// </summary>
public enum SourceType
{
    Unknown = 0,
    BirthCertificate = 1,
    DeathCertificate = 2,
    MarriageCertificate = 3,
    CensusRecord = 4,
    ChurchRecord = 5,
    MilitaryRecord = 6,
    Immigration = 7,
    Newspaper = 8,
    Book = 9,
    PersonalInterview = 10,
    Letter = 11,
    Diary = 12,
    Photo = 13,
    Video = 14,
    Audio = 15,
    Website = 16,
    Other = 99
}

/// <summary>
/// Conflicting information entry
/// </summary>
[Table("conflicting_information")]
public class ConflictingInformation : BaseEntity
{
    /// <summary>
    /// User who owns this conflict record (overrides base to add partition key)
    /// </summary>
    [PartitionKey(0)]
    public new Guid UserId { get; set; }

    [ClusteringKey(0)]
    public Guid EntityId { get; set; }

    [ClusteringKey(1)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// First value/claim
    /// </summary>
    public string Value1 { get; set; } = string.Empty;

    /// <summary>
    /// Second value/claim
    /// </summary>
    public string Value2 { get; set; } = string.Empty;

    /// <summary>
    /// Citation for first value
    /// </summary>
    public Guid? Citation1Id { get; set; }

    /// <summary>
    /// Citation for second value
    /// </summary>
    public Guid? Citation2Id { get; set; }

    /// <summary>
    /// Resolution strategy
    /// </summary>
    public ConflictResolutionStrategy Resolution { get; set; }

    /// <summary>
    /// Preferred value (if resolved)
    /// </summary>
    public string? PreferredValue { get; set; }

    /// <summary>
    /// Resolution notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When this was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Strategy for resolving conflicting information
/// </summary>
public enum ConflictResolutionStrategy
{
    Unresolved = 0,
    PreferNewer = 1,
    PreferOlder = 2,
    PreferHigherQuality = 3,
    ManualSelection = 4,
    KeepBoth = 5,
    Average = 6
}
