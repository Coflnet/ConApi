using Coflnet.Auth;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Controller for managing places
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlaceController : ControllerBase
{
    private readonly ILogger<PlaceController> _logger;
    private readonly PlaceService _placeService;
    private readonly SearchService _searchService;

    public PlaceController(ILogger<PlaceController> logger, PlaceService placeService, SearchService searchService)
    {
        _logger = logger;
        _placeService = placeService;
        _searchService = searchService;
    }

    /// <summary>
    /// Get a place by ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Place>> GetPlace(Guid id)
    {
        var userId = this.GetUserId();
        var place = await _placeService.GetPlaceById(userId, id);
        
        if (place == null)
            return NotFound();
            
        return Ok(place);
    }

    /// <summary>
    /// Get child places of a parent
    /// </summary>
    [HttpGet("{id}/children")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Place>>> GetChildPlaces(Guid id)
    {
        var userId = this.GetUserId();
        var children = await _placeService.GetChildPlaces(userId, id);
        return Ok(children);
    }

    /// <summary>
    /// Create or update a place
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Place>> CreatePlace([FromBody] PlaceDto dto)
    {
        var userId = this.GetUserId();
        
        var place = new Place
        {
            Id = dto.Id ?? Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            Type = dto.Type,
            ParentPlaceId = dto.ParentPlaceId,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            PrivacyLevel = dto.PrivacyLevel
        };
        
        var saved = await _placeService.SavePlace(place);
        
        // Add to search index
        await _searchService.AddEntry(userId, saved.Name, saved.Id.ToString(), SearchEntry.ResultType.Location);
        
        return CreatedAtAction(nameof(GetPlace), new { id = saved.Id }, saved);
    }

    /// <summary>
    /// Add flexible attribute to a place
    /// </summary>
    [HttpPost("{id}/data")]
    [Authorize]
    public async Task<IActionResult> AddPlaceData(Guid id, [FromBody] PersonAttributeDto dto)
    {
        var userId = this.GetUserId();
        await _placeService.UpsertAttribute(userId, id, string.Empty, dto.Key, dto.Value);
        return Ok();
    }

    /// <summary>
    /// Get all flexible attributes for a place
    /// </summary>
    [HttpGet("{id}/data")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<PlaceData>>> GetPlaceData(Guid id, [FromQuery] string? category = null)
    {
        var userId = this.GetUserId();
        var attrs = await _placeService.GetAttributesByPlaceId(userId, id);
        return Ok(attrs);
    }
}
