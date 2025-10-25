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

    public PersonController(ILogger<PersonController> logger, PersonService personService, SearchService searchService, RelationshipService relationshipService, ThingService thingService, EventService eventService)
    {
        _logger = logger;
        _personService = personService;
        _searchService = searchService;
        _relationshipService = relationshipService;
        _thingService = thingService;
        _eventService = eventService;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IEnumerable<PersonAttributeDto>> GetPersonData(string id)
    {
        var userId = this.GetUserId();
        var parts = id.Split(";");
        if (parts.Length == 1)
        {
            if (Guid.TryParse(parts[0], out var pid))
            {
                var attrs = await _personService.GetAttributesByPersonId(userId, pid);
                return attrs.Select(kv => new PersonAttributeDto { PersonId = pid, Key = kv.Key, Value = kv.Value, Category = string.Empty });
            }
            var attrs2 = await _personService.GetAttributesByName(userId, parts[0]);
            return attrs2.Select(kv => new PersonAttributeDto { PersonId = Guid.Empty, Key = kv.Key, Value = kv.Value, Category = string.Empty });
        }
        else
        {
            // legacy composite id handling — treat as name-based query
            var attrs = await _personService.GetAttributesByName(userId, parts[0]);
            return attrs.Select(kv => new PersonAttributeDto { PersonId = Guid.Empty, Key = kv.Key, Value = kv.Value, Category = string.Empty });
        }
    }

    /// <summary>
    /// Get a full view of the person including flexible data, relationships, things and events
    /// </summary>
    [HttpGet("view/{id}")]
    [Authorize]
    public async Task<IActionResult> GetPersonView(string id)
    {
        var userId = this.GetUserId();

        IEnumerable<PersonAttributeDto> personData;
        if (Guid.TryParse(id, out var pid))
        {
            var attrs = await _personService.GetAttributesByPersonId(userId, pid);
            personData = attrs.Select(kv => new PersonAttributeDto { PersonId = pid, Key = kv.Key, Value = kv.Value, Category = string.Empty });
        }
        else
        {
            var parts = id.Split(";");
            var attrs = await _personService.GetAttributesByName(userId, parts[0]);
            personData = attrs.Select(kv => new PersonAttributeDto { PersonId = Guid.Empty, Key = kv.Key, Value = kv.Value, Category = string.Empty });
        }

        // If we have a GUID person id, use GUID calls; otherwise relationships/things/events can't be resolved by GUID
        IEnumerable<Relationship> relationships = new List<Relationship>();
        IEnumerable<Thing> things = new List<Thing>();
        IEnumerable<Event> events = new List<Event>();

        if (Guid.TryParse(id, out var pid2))
        {
            relationships = await _relationshipService.GetRelationshipsForEntity(userId, pid2);
            things = await _thingService.GetThingsByOwner(userId, pid2);
            events = await _eventService.GetTimeline(userId, pid2);
        }

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

        if (string.Equals(data.Key, "name", StringComparison.OrdinalIgnoreCase))
        {
            // Build composite id for search: try to obtain birthday/birthplace from attributes if available
            string date = string.Empty;
            string birthPlace = string.Empty;
            if (personId.HasValue)
            {
                var attrs = await _personService.GetAttributesByPersonId(userId, personId.Value);
                if (attrs.TryGetValue("birthday", out var b)) date = b;
                if (attrs.TryGetValue("birthplace", out var bp)) birthPlace = bp;
            }
            var fullId = $"{data.Value};{date};{birthPlace}";
            await _searchService.AddEntry(userId, data.Value, fullId, SearchEntry.ResultType.Person);
        }
    }
}