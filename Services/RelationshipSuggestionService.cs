using Coflnet.Connections.DTOs;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for suggesting potential relationships based on shared events and places
/// </summary>
public class RelationshipSuggestionService
{
    private readonly PersonService _personService;
    private readonly EventService _eventService;
    private readonly RelationshipService _relationshipService;
    private readonly ILogger<RelationshipSuggestionService> _logger;

    public RelationshipSuggestionService(
        PersonService personService,
        EventService eventService,
        RelationshipService relationshipService,
        ILogger<RelationshipSuggestionService> logger)
    {
        _personService = personService;
        _eventService = eventService;
        _relationshipService = relationshipService;
        _logger = logger;
    }

    /// <summary>
    /// Get relationship suggestions for a person based on shared events and places
    /// </summary>
    public async Task<List<RelationshipSuggestion>> GetSuggestions(Guid userId, Guid personId)
    {
        _logger.LogInformation("Generating relationship suggestions for person {PersonId}", personId);

        // Get person's events
        var events = await _eventService.GetTimeline(userId, personId);
        
        // Get existing relationships to exclude
        var existingRelationships = await _relationshipService.GetRelationshipsForEntity(userId, personId);
        var existingRelatedIds = new HashSet<Guid>(
            existingRelationships.Select(r => r.ToEntityId != personId ? r.ToEntityId : r.FromEntityId)
        );

        var suggestions = new Dictionary<Guid, RelationshipSuggestion>();

        // Find people who attended same events or were at same places
        foreach (var evt in events)
        {
            // Get participants of this event
            var participants = await _eventService.GetParticipants(userId, evt.Id);
            
            foreach (var participantId in participants.Where(p => p != personId && !existingRelatedIds.Contains(p)))
            {
                if (!suggestions.TryGetValue(participantId, out var suggestion))
                {
                    var attrs = await _personService.GetAttributesByPersonId(userId, participantId);
                    var name = attrs.TryGetValue("name", out var n) ? n : "Unknown";
                    suggestion = new RelationshipSuggestion
                    {
                        PersonId = participantId,
                        PersonName = name,
                        SharedEvents = new List<string>(),
                        SharedPlaces = new List<string>()
                    };
                    suggestions[participantId] = suggestion;
                }

                suggestion.SharedEvents.Add($"{evt.Type} on {evt.EventDate:yyyy-MM-dd}");
            }
        }

        // Calculate confidence scores and suggest relationship types
        var result = new List<RelationshipSuggestion>();
        foreach (var suggestion in suggestions.Values)
        {
            var sharedEventCount = suggestion.SharedEvents.Count;
            var sharedPlaceCount = suggestion.SharedPlaces.Count;

            // Calculate confidence: more shared events/places = higher confidence
            suggestion.ConfidenceScore = Math.Min(1.0, (sharedEventCount * 0.3 + sharedPlaceCount * 0.2));

            // Suggest relationship type based on patterns
            suggestion.SuggestedRelationType = InferRelationshipType(sharedEventCount, sharedPlaceCount);
            suggestion.Reason = BuildReason(sharedEventCount, sharedPlaceCount);

            result.Add(suggestion);
        }

        // Sort by confidence score
        return result.OrderByDescending(s => s.ConfidenceScore)
            .Take(20) // Limit to top 20 suggestions
            .ToList();
    }

    private string InferRelationshipType(int sharedEvents, int sharedPlaces)
    {
        if (sharedEvents >= 5)
            return "Friend"; // Many shared events suggests friendship
        if (sharedEvents >= 2)
            return "Acquaintance";
        if (sharedPlaces >= 3)
            return "Neighbor"; // Same places suggests geographical proximity
        
        return "Related"; // Generic relationship
    }

    private string BuildReason(int sharedEvents, int sharedPlaces)
    {
        var reasons = new List<string>();
        
        if (sharedEvents > 0)
            reasons.Add($"{sharedEvents} shared event{(sharedEvents > 1 ? "s" : "")}");
        if (sharedPlaces > 0)
            reasons.Add($"{sharedPlaces} shared location{(sharedPlaces > 1 ? "s" : "")}");

        return reasons.Any() 
            ? string.Join(" and ", reasons)
            : "No specific connection found";
    }
}
