using Coflnet.Connections.Models;

namespace Coflnet.Connections.DTOs;

/// <summary>
/// DTO for creating a share invitation
/// </summary>
public class CreateShareInvitationDto
{
    public Guid ToUserId { get; set; }
    public EntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public SharePermission Permission { get; set; }
    public string? Message { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// DTO for responding to a share invitation
/// </summary>
public class RespondToInvitationDto
{
    public bool Accept { get; set; }
    public ConflictResolution? DefaultConflictResolution { get; set; }
}

/// <summary>
/// DTO for data export request
/// </summary>
public class ExportRequestDto
{
    public bool IncludePersons { get; set; } = true;
    public bool IncludePlaces { get; set; } = true;
    public bool IncludeThings { get; set; } = true;
    public bool IncludeEvents { get; set; } = true;
    public bool IncludeRelationships { get; set; } = true;
    public bool IncludeDocuments { get; set; } = true;
    public ExportFormat Format { get; set; } = ExportFormat.Json;
}

/// <summary>
/// Export format
/// </summary>
public enum ExportFormat
{
    Json = 0,
    Gedcom = 1
}

/// <summary>
/// DTO for conflict resolution
/// </summary>
public class ResolveConflictDto
{
    public Guid ConflictId { get; set; }
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public ConflictResolution Resolution { get; set; }
    public string? ResolvedValue { get; set; }
}

/// <summary>
/// DTO for document upload
/// </summary>
public class UploadDocumentDto
{
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DocumentType Type { get; set; }
    public DateTime? DocumentDate { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// DTO for linking document to entity
/// </summary>
public class LinkDocumentDto
{
    public Guid DocumentId { get; set; }
    public Guid EntityId { get; set; }
    public EntityType EntityType { get; set; }
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// DTO for presigned URL response
/// </summary>
public class PresignedUrlDto
{
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string? FileName { get; set; }
}
