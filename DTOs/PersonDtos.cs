namespace Coflnet.Connections.DTOs;

/// <summary>
/// Full person view with all related data
/// </summary>
public class PersonFullView
{
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<RelationshipSummaryDto> Relationships { get; set; } = new();
    public List<EventSummaryDto> Events { get; set; } = new();
    public List<PlaceSummaryDto> Places { get; set; } = new();
    public List<ThingSummaryDto> Things { get; set; } = new();
}

/// <summary>
/// Timeline entry for person
/// </summary>
public class TimelineEntry
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty; // Birth, Death, Event, Relationship
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Person timeline view
/// </summary>
public class PersonTimeline
{
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TimelineEntry> Timeline { get; set; } = new();
}

/// <summary>
/// Relationship summary (lightweight version)
/// </summary>
public class RelationshipSummaryDto
{
    public Guid RelationshipId { get; set; }
    public Guid RelatedPersonId { get; set; }
    public string RelatedPersonName { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Event summary (lightweight version)
/// </summary>
public class EventSummaryDto
{
    public Guid EventId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public Guid? PlaceId { get; set; }
    public string? PlaceName { get; set; }
}

/// <summary>
/// Place summary (lightweight version)
/// </summary>
public class PlaceSummaryDto
{
    public Guid PlaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>
/// Thing summary (lightweight version)
/// </summary>
public class ThingSummaryDto
{
    public Guid ThingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Bulk person creation request
/// </summary>
public class BulkPersonRequest
{
    public List<PersonCreateDto> People { get; set; } = new();
}

/// <summary>
/// Single person creation data
/// </summary>
public class PersonCreateDto
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// Bulk operation result
/// </summary>
public class BulkOperationResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<Guid> CreatedIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Relationship suggestion
/// </summary>
public class RelationshipSuggestion
{
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string SuggestedRelationType { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } // 0-1
    public string Reason { get; set; } = string.Empty;
    public List<string> SharedEvents { get; set; } = new();
    public List<string> SharedPlaces { get; set; } = new();
}
