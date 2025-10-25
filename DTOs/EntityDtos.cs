using Coflnet.Connections.Models;

namespace Coflnet.Connections.DTOs;

/// <summary>
/// DTO for creating/updating a person
/// </summary>
public class PersonDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Birthday { get; set; }
    public string BirthPlace { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Gender Gender { get; set; }
    public DateTime? DeathDate { get; set; }
    public string? DeathPlace { get; set; }
    public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;
}

/// <summary>
/// DTO for adding flexible attributes to a person
/// </summary>
public class PersonAttributeDto
{
    // Allow composite or string IDs (e.g., Name;YYYY-MM-DD;BirthPlace)
    public Guid? PersonId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for place creation/update
/// </summary>
public class PlaceDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlaceType Type { get; set; }
    public Guid? ParentPlaceId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;
}

/// <summary>
/// DTO for thing creation/update
/// </summary>
public class ThingDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ThingType Type { get; set; }
    // OwnerId references the owner's GUID
    public Guid? OwnerId { get; set; }
    public string? Manufacturer { get; set; }
    public int? YearMade { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;
}

/// <summary>
/// DTO for event creation/update
/// </summary>
public class EventDto
{
    public Guid? Id { get; set; }
    public EventType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Description { get; set; }
    public EntityType TargetEntityType { get; set; }
    // Target entity referenced by GUID
    public Guid TargetEntityId { get; set; }
    public Guid? PlaceId { get; set; }
    public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Private;
}

/// <summary>
/// DTO for relationship creation/update
/// </summary>
public class RelationshipDto
{
    public Guid? Id { get; set; }
    public EntityType FromEntityType { get; set; }
    // Use GUID ids for entity references
    public Guid FromEntityId { get; set; }
    public EntityType ToEntityType { get; set; }
    public Guid ToEntityId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;
    public string Language { get; set; } = "de";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Certainty { get; set; } = 100;
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for search requests with filtering
/// </summary>
public class SearchRequestDto
{
    public string Query { get; set; } = string.Empty;
    public EntityType? EntityType { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// DTO for timeline requests
/// </summary>
public class TimelineRequestDto
{
    public EntityType EntityType { get; set; }
    // Composite/string id allowed
    public Guid EntityId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
