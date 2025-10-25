using Coflnet.Auth;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Controller for managing things (physical objects)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ThingController : ControllerBase
{
    private readonly ILogger<ThingController> _logger;
    private readonly ThingService _thingService;
    private readonly SearchService _searchService;

    public ThingController(ILogger<ThingController> logger, ThingService thingService, SearchService searchService)
    {
        _logger = logger;
        _thingService = thingService;
        _searchService = searchService;
    }

    /// <summary>
    /// Get a thing by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Thing>> GetThing(Guid id)
    {
        var userId = this.GetUserId();
        var thing = await _thingService.GetThingById(userId, id);
        
        if (thing == null)
            return NotFound();
            
        return Ok(thing);
    }

    /// <summary>
    /// Get things owned by a person
    /// </summary>
    [HttpGet("owner/{ownerId}")]
    [Authorize]
        public async Task<ActionResult<IEnumerable<Thing>>> GetThingsByOwner(Guid ownerId)
    {
        var userId = this.GetUserId();
        var things = await _thingService.GetThingsByOwner(userId, ownerId);
        return Ok(things);
    }

    /// <summary>
    /// Create or update a thing
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Thing>> CreateThing([FromBody] ThingDto dto)
    {
        var userId = this.GetUserId();
        
        var thing = new Thing
        {
            Id = dto.Id ?? Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Type = dto.Type,
            OwnerId = dto.OwnerId,
            Manufacturer = dto.Manufacturer,
            YearMade = dto.YearMade,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            PrivacyLevel = dto.PrivacyLevel
        };
        
        var saved = await _thingService.SaveThing(thing);
        
        // Add to search index
        await _searchService.AddEntry(userId, saved.Name, saved.Id.ToString(), SearchEntry.ResultType.Unknown);
        
        return CreatedAtAction(nameof(GetThing), new { id = saved.Id }, saved);
    }

    /// <summary>
    /// Add flexible attribute to a thing
    /// </summary>
    [HttpPost("{id}/data")]
    [Authorize]
    public async Task<IActionResult> AddThingData(Guid id, [FromBody] PersonAttributeDto dto)
    {
        var userId = this.GetUserId();
            await _thingService.UpsertAttribute(userId, id, string.Empty, dto.Key, dto.Value);
        return Ok();
    }

    /// <summary>
    /// Get all flexible attributes for a thing
    /// </summary>
    [HttpGet("{id}/data")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ThingData>>> GetThingData(Guid id, [FromQuery] string? category = null)
    {
        var userId = this.GetUserId();
        var attrs = await _thingService.GetAttributesByThingId(userId, id);
        return Ok(attrs);
    }
}
