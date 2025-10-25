using Coflnet.Auth;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Models;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Controller for managing events and timelines
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    private readonly ILogger<EventController> _logger;
    private readonly EventService _eventService;
    private readonly SearchService _searchService;

    public EventController(ILogger<EventController> logger, EventService eventService, SearchService searchService)
    {
        _logger = logger;
        _eventService = eventService;
        _searchService = searchService;
    }

    /// <summary>
    /// Get an event by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Event>> GetEvent(Guid id)
    {
        var userId = this.GetUserId();
        var ev = await _eventService.GetEventById(userId, id);
        
        if (ev == null)
            return NotFound();
            
        return Ok(ev);
    }

    /// <summary>
    /// Get timeline for an entity (person, place, or thing)
    /// </summary>
    [HttpPost("timeline")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Event>>> GetTimeline([FromBody] TimelineRequestDto request)
    {
        var userId = this.GetUserId();
        var events = await _eventService.GetTimeline(
            userId, 
            request.EntityId, 
            request.StartDate, 
            request.EndDate);
        
        return Ok(events);
    }

    /// <summary>
    /// Get events by type
    /// </summary>
    [HttpGet("type/{type}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Event>>> GetEventsByType(EventType type, [FromQuery] int limit = 100)
    {
        var userId = this.GetUserId();
        var events = await _eventService.GetEventsByType(userId, type, limit);
        return Ok(events);
    }

    /// <summary>
    /// Get events in a date range
    /// </summary>
    [HttpGet("range")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Event>>> GetEventsByDateRange(
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate)
    {
        var userId = this.GetUserId();
        var events = await _eventService.GetEventsByDateRange(userId, startDate, endDate);
        return Ok(events);
    }

    /// <summary>
    /// Create or update an event
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Event>> CreateEvent([FromBody] EventDto dto)
    {
        var userId = this.GetUserId();
        
        var ev = new Event
        {
            Id = dto.Id ?? Guid.NewGuid(),
            UserId = userId,
            Type = dto.Type,
            Title = dto.Title,
            EventDate = dto.EventDate,
            EndDate = dto.EndDate,
            Description = dto.Description,
            TargetEntityType = dto.TargetEntityType,
            TargetEntityId = dto.TargetEntityId,
            PlaceId = dto.PlaceId,
            PrivacyLevel = dto.PrivacyLevel
        };
        
        var saved = await _eventService.SaveEvent(ev);
        
        // Add to search index
        await _searchService.AddEntry(userId, saved.Title, saved.Id.ToString(), SearchEntry.ResultType.Unknown);
        
        return CreatedAtAction(nameof(GetEvent), new { id = saved.Id }, saved);
    }

    /// <summary>
    /// Add flexible attribute to an event
    /// </summary>
    [HttpPost("{id}/data")]
    [Authorize]
    public async Task<IActionResult> AddEventData(Guid id, [FromBody] PersonAttributeDto dto)
    {
        var userId = this.GetUserId();
        await _eventService.UpsertAttribute(userId, id, string.Empty, dto.Key, dto.Value);
        return Ok();
    }

    /// <summary>
    /// Get all flexible attributes for an event
    /// </summary>
    [HttpGet("{id}/data")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EventData>>> GetEventData(Guid id)
    {
        var userId = this.GetUserId();
        var attrs = await _eventService.GetAttributesByEventId(userId, id);
        return Ok(attrs);
    }
}
