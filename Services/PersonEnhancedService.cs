using Coflnet.Connections.DTOs;
using Coflnet.Connections.Models;

namespace Coflnet.Connections.Services;

/// <summary>
/// Enhanced person service for optimized full data retrieval
/// </summary>
public class PersonEnhancedService
{
    private readonly PersonService _personService;
    private readonly RelationshipService _relationshipService;
    private readonly EventService _eventService;
    private readonly PlaceService _placeService;
    private readonly ThingService _thingService;
    private readonly ILogger<PersonEnhancedService> _logger;

    public PersonEnhancedService(
        PersonService personService,
        RelationshipService relationshipService,
        EventService eventService,
        PlaceService placeService,
        ThingService thingService,
        ILogger<PersonEnhancedService> logger)
    {
        _personService = personService;
        _relationshipService = relationshipService;
        _eventService = eventService;
        _placeService = placeService;
        _thingService = thingService;
        _logger = logger;
    }

    /// <summary>
    /// Get full person view with all related data in a single optimized call
    /// </summary>
    public async Task<PersonFullView> GetPersonFull(Guid userId, Guid personId)
    {
        _logger.LogInformation("Building full person view for {PersonId}", personId);

        // Parallel fetch all data
        var attributesTask = _personService.GetAttributesByPersonId(userId, personId);
        var relationshipsTask = _relationshipService.GetRelationshipsForEntity(userId, personId);
        var eventsTask = _eventService.GetTimeline(userId, personId);
        var thingsTask = _thingService.GetThingsByOwner(userId, personId);

        await Task.WhenAll(attributesTask, relationshipsTask, eventsTask, thingsTask);

        var attributes = await attributesTask;
        var relationships = (await relationshipsTask).ToList(); // Materialize to avoid multiple enumeration
        var events = await eventsTask;
        var things = await thingsTask;

        // Get unique place IDs from events
        var placeIds = events
            .Where(e => e.PlaceId.HasValue && e.PlaceId.Value != Guid.Empty)
            .Select(e => e.PlaceId.Value)
            .Distinct()
            .ToList();

        // Fetch places in parallel
        var placeTasks = placeIds.Select(placeId => 
            _placeService.GetPlaceById(userId, placeId));
        var placesArray = await Task.WhenAll(placeTasks);
        var places = placesArray.Where(p => p != null).Select(p => p!).ToList();

        // Get names for related people
        var relatedPersonIds = relationships
            .Select(r => r.ToEntityId != personId ? r.ToEntityId : r.FromEntityId)
            .Distinct()
            .ToList();

        var personNameTasks = relatedPersonIds.Select(async id =>
        {
            var attrs = await _personService.GetAttributesByPersonId(userId, id);
            var name = attrs.TryGetValue("name", out var n) ? n : "Unknown";
            return (Id: id, Name: name);
        });
        var personNames = await Task.WhenAll(personNameTasks);
        var nameDict = personNames.ToDictionary(x => x.Id, x => x.Name);

        // Build DTOs
        var relationshipDtos = relationships.Select(r =>
        {
            var relatedId = r.ToEntityId != personId ? r.ToEntityId : r.FromEntityId;
            return new RelationshipSummaryDto
            {
                RelationshipId = r.Id,
                RelatedPersonId = relatedId,
                RelatedPersonName = nameDict.TryGetValue(relatedId, out var name) ? name : "Unknown",
                RelationType = r.RelationshipType,
                StartDate = r.StartDate,
                EndDate = r.EndDate
            };
        }).ToList();

        var placeDict = places.ToDictionary(p => p.Id);
        var eventDtos = events.Select(e => new EventSummaryDto
        {
            EventId = e.Id,
            Type = e.Type.ToString(),
            Description = e.Description ?? string.Empty,
            Date = e.EventDate,
            PlaceId = e.PlaceId,
            PlaceName = e.PlaceId.HasValue && placeDict.TryGetValue(e.PlaceId.Value, out var place) 
                ? place.Name 
                : null
        }).ToList();

        var placeDtos = places.Select(p => new PlaceSummaryDto
        {
            PlaceId = p.Id,
            Name = p.Name,
            Address = null, // Place model doesn't have Address field
            Latitude = p.Latitude == 0 ? null : p.Latitude,
            Longitude = p.Longitude == 0 ? null : p.Longitude
        }).ToList();

        var thingDtos = things.Select(t => new ThingSummaryDto
        {
            ThingId = t.Id,
            Name = t.Name,
            Type = t.Type.ToString(),
            Description = t.Model ?? t.Manufacturer
        }).ToList();

        return new PersonFullView
        {
            PersonId = personId,
            Name = attributes.TryGetValue("name", out var personName) ? personName : "Unknown",
            Attributes = new Dictionary<string, string>(attributes),
            Relationships = relationshipDtos,
            Events = eventDtos,
            Places = placeDtos,
            Things = thingDtos
        };
    }

    /// <summary>
    /// Get person timeline with all events chronologically ordered
    /// </summary>
    public async Task<PersonTimeline> GetPersonTimeline(Guid userId, Guid personId)
    {
        _logger.LogInformation("Building timeline for person {PersonId}", personId);

        var attributesTask = _personService.GetAttributesByPersonId(userId, personId);
        var eventsTask = _eventService.GetTimeline(userId, personId);
        var relationshipsTask = _relationshipService.GetRelationshipsForEntity(userId, personId);

        await Task.WhenAll(attributesTask, eventsTask, relationshipsTask);

        var attributes = await attributesTask;
        var events = await eventsTask;
        var relationships = await relationshipsTask;

        var timeline = new List<TimelineEntry>();

        // Add birth event if available
        if (attributes.TryGetValue("birthday", out var birthday) && DateTime.TryParse(birthday, out var birthDate))
        {
            var birthplace = attributes.TryGetValue("birthplace", out var bp) ? bp : null;
            timeline.Add(new TimelineEntry
            {
                Date = birthDate,
                Type = "Birth",
                Title = "Born",
                Description = birthplace != null ? $"Born in {birthplace}" : "Birth",
                Location = birthplace
            });
        }

        // Add death event if available
        if (attributes.TryGetValue("deathday", out var deathday) && DateTime.TryParse(deathday, out var deathDate))
        {
            var deathplace = attributes.TryGetValue("deathplace", out var dp) ? dp : null;
            timeline.Add(new TimelineEntry
            {
                Date = deathDate,
                Type = "Death",
                Title = "Died",
                Description = deathplace != null ? $"Died in {deathplace}" : "Death",
                Location = deathplace
            });
        }

        // Add events
        foreach (var evt in events)
        {
            string? location = null;
            if (evt.PlaceId.HasValue && evt.PlaceId.Value != Guid.Empty)
            {
                var place = await _placeService.GetPlaceById(userId, evt.PlaceId.Value);
                location = place?.Name;
            }

            timeline.Add(new TimelineEntry
            {
                Date = evt.EventDate,
                Type = "Event",
                Title = evt.Type.ToString(),
                Description = evt.Description ?? string.Empty,
                RelatedEntityId = evt.Id,
                Location = location
            });
        }

        // Add relationship start dates
        foreach (var rel in relationships.Where(r => r.StartDate.HasValue))
        {
            var relatedId = rel.ToEntityId != personId ? rel.ToEntityId : rel.FromEntityId;
            var relatedAttrs = await _personService.GetAttributesByPersonId(userId, relatedId);
            var relatedName = relatedAttrs.TryGetValue("name", out var rn) ? rn : "Unknown";

            timeline.Add(new TimelineEntry
            {
                Date = rel.StartDate.Value,
                Type = "Relationship",
                Title = $"{rel.RelationshipType} with {relatedName}",
                Description = $"Relationship started: {rel.RelationshipType}",
                RelatedEntityId = relatedId
            });
        }

        // Sort chronologically
        timeline = timeline.OrderBy(t => t.Date).ToList();

        return new PersonTimeline
        {
            PersonId = personId,
            Name = attributes.TryGetValue("name", out var pName) ? pName : "Unknown",
            Timeline = timeline
        };
    }
}
