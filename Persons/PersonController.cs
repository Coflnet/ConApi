using Coflnet.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Coflnet.Connections.DTOs;

namespace Coflnet.Connections.Services;

[ApiController]
[Route("api/[controller]")]
public class PersonController : ControllerBase
{
    private readonly ILogger<PersonController> _logger;
    private readonly PersonService _personService;
    private readonly SearchService _searchService;
    private readonly RelationshipService _relationshipService;
    private readonly ThingService _thingService;
    private readonly EventService _eventService;
    private readonly PersonEnhancedService _personEnhancedService;

    public PersonController(
        ILogger<PersonController> logger, 
        PersonService personService, 
        SearchService searchService, 
        RelationshipService relationshipService, 
        ThingService thingService, 
        EventService eventService,
        PersonEnhancedService personEnhancedService)
    {
        _logger = logger;
        _personService = personService;
        _searchService = searchService;
        _relationshipService = relationshipService;
        _thingService = thingService;
        _eventService = eventService;
        _personEnhancedService = personEnhancedService;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IEnumerable<PersonAttributeDto>> GetPersonData(string id)
    {
        var userId = this.GetUserId();
        
        // Only support UUID-based lookups now
        if (Guid.TryParse(id, out var pid))
        {
            var attrs = await _personService.GetAttributesByPersonId(userId, pid);
            return attrs.Select(kv => new PersonAttributeDto { PersonId = pid, Key = kv.Key, Value = kv.Value, Category = string.Empty });
        }
        
        // Return empty if not a valid UUID
        return Array.Empty<PersonAttributeDto>();
    }

    /// <summary>
    /// Get a full view of the person including flexible data, relationships, things and events
    /// </summary>
    [HttpGet("view/{id}")]
    [Authorize]
    public async Task<IActionResult> GetPersonView(string id)
    {
        var userId = this.GetUserId();

        // Only support UUID-based lookups now
        if (!Guid.TryParse(id, out var pid))
        {
            return BadRequest("Person ID must be a valid UUID");
        }

        var attrs = await _personService.GetAttributesByPersonId(userId, pid);
        var personData = attrs.Select(kv => new PersonAttributeDto { PersonId = pid, Key = kv.Key, Value = kv.Value, Category = string.Empty });

        var relationships = await _relationshipService.GetRelationshipsForEntity(userId, pid);
        var things = await _thingService.GetThingsByOwner(userId, pid);
        var events = await _eventService.GetTimeline(userId, pid);

        return Ok(new {
            PersonData = personData,
            Relationships = relationships,
            Things = things,
            Events = events
        });
    }

    [HttpPost]
    [Authorize]
    public async Task AddPersonData(PersonAttributeDto data)
    {
        var userId = this.GetUserId();
        var personId = data.PersonId == Guid.Empty ? (Guid?)null : data.PersonId;

        // Determine name to provide to UpsertAttribute when creating a new person row.
        var nameToUse = string.Empty;
        if (string.Equals(data.Key, "name", StringComparison.OrdinalIgnoreCase))
        {
            nameToUse = data.Value;
        }

        await _personService.UpsertAttribute(userId, personId, nameToUse, data.Key, data.Value);

        if (string.Equals(data.Key, "name", StringComparison.OrdinalIgnoreCase) && personId.HasValue)
        {
            // Add to search with UUID only
            await _searchService.AddEntry(userId, data.Value, personId.Value.ToString(), SearchEntry.ResultType.Person);
        }
    }

    /// <summary>
    /// Get complete person data with all relationships, events, and places in one optimized call
    /// FAST: Optimized for frontend performance with parallel data fetching and caching
    /// </summary>
    [HttpGet("{id}/full")]
    [Authorize]
    public async Task<ActionResult<PersonFullView>> GetPersonFull(Guid id)
    {
        var userId = this.GetUserId();
        _logger.LogInformation("Getting full person view for {PersonId}", id);

        try
        {
            var result = await _personEnhancedService.GetPersonFull(userId, id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting full person view for {PersonId}", id);
            return StatusCode(500, "Failed to retrieve person data");
        }
    }

    /// <summary>
    /// Get chronological timeline for a person with all life events
    /// FAST: Pre-built timeline data optimized for frontend display
    /// </summary>
    [HttpGet("{id}/timeline")]
    [Authorize]
    public async Task<ActionResult<PersonTimeline>> GetPersonTimeline(Guid id)
    {
        var userId = this.GetUserId();
        _logger.LogInformation("Getting timeline for person {PersonId}", id);

        try
        {
            var result = await _personEnhancedService.GetPersonTimeline(userId, id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting timeline for person {PersonId}", id);
            return StatusCode(500, "Failed to retrieve timeline");
        }
    }

    /// <summary>
    /// Create multiple people at once
    /// </summary>
    [HttpPost("bulk")]
    [Authorize]
    public async Task<ActionResult<BulkOperationResult>> CreateBulk([FromBody] BulkPersonRequest request)
    {
        var userId = this.GetUserId();
        _logger.LogInformation("Bulk creating {Count} people for user {UserId}", request.People.Count, userId);

        var result = new BulkOperationResult();

        foreach (var personData in request.People)
        {
            try
            {
                // Create person with generated UUID
                var newPersonId = Guid.NewGuid();
                await _personService.UpsertAttribute(userId, newPersonId, personData.Name, "name", personData.Name);

                // Add all attributes
                foreach (var attr in personData.Attributes)
                {
                    await _personService.UpsertAttribute(userId, newPersonId, personData.Name, attr.Key, attr.Value);
                }

                // Add to search with UUID only
                await _searchService.AddEntry(userId, personData.Name, newPersonId.ToString(), SearchEntry.ResultType.Person);

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create person: {Name}", personData.Name);
                result.FailureCount++;
                result.Errors.Add($"Failed to create {personData.Name}: {ex.Message}");
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Test endpoint that creates a person and immediately fetches their full view to test schema initialization.
    /// </summary>
    [HttpPost("test-full-view")]
    [Authorize]
    public async Task<ActionResult<object>> TestFullViewFlow()
    {
        var userId = this.GetUserId();
        _logger.LogInformation("Testing full person view flow for user {UserId}", userId);

        try
        {
            // 1. Create a test person
            var testPersonId = Guid.NewGuid();
            var testPersonName = $"Test Person {DateTime.UtcNow:yyyyMMddHHmmss}";

            await _personService.UpsertAttribute(userId, testPersonId, testPersonName, "name", testPersonName);
            await _personService.UpsertAttribute(userId, testPersonId, testPersonName, "birthday", "1990-01-01");

            _logger.LogInformation("Created test person {PersonId} for user {UserId}", testPersonId, userId);

            // 2. Immediately fetch full view (tests schema readiness)
            var fullView = await _personEnhancedService.GetPersonFull(userId, testPersonId);

            _logger.LogInformation("Successfully fetched full person view for test person {PersonId}", testPersonId);

            return Ok(new
            {
                Success = true,
                PersonId = testPersonId,
                PersonName = testPersonName,
                FullView = fullView,
                Message = "Full view test passed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test full view flow failed for user {UserId}", userId);
            return StatusCode(500, new
            {
                Success = false,
                Error = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }
}