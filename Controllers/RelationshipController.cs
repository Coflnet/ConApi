using Coflnet.Auth;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Controller for managing relationships between entities
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RelationshipController : ControllerBase
{
    private readonly ILogger<RelationshipController> _logger;
    private readonly RelationshipService _relationshipService;

    public RelationshipController(ILogger<RelationshipController> logger, RelationshipService relationshipService)
    {
        _logger = logger;
        _relationshipService = relationshipService;
    }

    /// <summary>
    /// Get all relationships for an entity
    /// </summary>
    [HttpGet("entity/{entityId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsForEntity(
        Guid entityId, 
        [FromQuery] bool primaryOnly = false)
    {
        var userId = this.GetUserId();
        var relationships = await _relationshipService.GetRelationshipsForEntity(userId, entityId, primaryOnly);
        return Ok(relationships);
    }

    /// <summary>
    /// Get specific relationship between two entities
    /// </summary>
    [HttpGet("between")]
    [Authorize]
    public async Task<ActionResult<Relationship>> GetRelationship(
        [FromQuery] Guid fromId, 
        [FromQuery] Guid toId, 
        [FromQuery] string? type = null)
    {
        var userId = this.GetUserId();
        var relationship = await _relationshipService.GetRelationship(userId, fromId, toId, type);
        
        if (relationship == null)
            return NotFound();
            
        return Ok(relationship);
    }

    /// <summary>
    /// Get all relationships of a specific type for an entity
    /// </summary>
    [HttpGet("entity/{entityId}/type/{relationshipType}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Relationship>>> GetRelationshipsByType(
        Guid entityId, 
        string relationshipType)
    {
        var userId = this.GetUserId();
        var relationships = await _relationshipService.GetRelationshipsByType(userId, entityId, relationshipType);
        return Ok(relationships);
    }

    /// <summary>
    /// Find relationship path between two entities (e.g., "John's Uncle")
    /// </summary>
    [HttpGet("path")]
    [Authorize]
    public async Task<ActionResult<List<Relationship>>> FindRelationshipPath(
        [FromQuery] Guid startEntityId, 
        [FromQuery] Guid endEntityId, 
        [FromQuery] int maxDepth = 3)
    {
        var userId = this.GetUserId();
        var path = await _relationshipService.FindRelationshipPath(userId, startEntityId, endEntityId, maxDepth);
        return Ok(path);
    }

    /// <summary>
    /// Create a bidirectional relationship
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Relationship>> CreateRelationship([FromBody] RelationshipDto dto)
    {
        var userId = this.GetUserId();
        
        var (primary, inverse) = await _relationshipService.CreateRelationship(
            userId,
            dto.FromEntityType, dto.FromEntityId,
            dto.ToEntityType, dto.ToEntityId,
            dto.RelationshipType,
            dto.Language,
            dto.StartDate,
            dto.EndDate,
            dto.Certainty,
            dto.Source,
            dto.Notes);
        
        return CreatedAtAction(
            nameof(GetRelationship), 
            new { fromId = primary.FromEntityId, toId = primary.ToEntityId, type = primary.RelationshipType }, 
            primary);
    }

    /// <summary>
    /// Update a relationship
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateRelationship(Guid id, [FromBody] RelationshipDto dto)
    {
        var userId = this.GetUserId();
        
        // Get existing relationship
        var existing = await _relationshipService.GetRelationship(userId, dto.FromEntityId, dto.ToEntityId, dto.RelationshipType);
        if (existing == null)
            return NotFound();
        
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;
        existing.Certainty = dto.Certainty;
        existing.Source = dto.Source;
        existing.Notes = dto.Notes;
        
        await _relationshipService.UpdateRelationship(existing);
        return Ok();
    }

    /// <summary>
    /// Delete a relationship
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteRelationship(Guid id)
    {
        var userId = this.GetUserId();
        await _relationshipService.DeleteRelationship(userId, id);
        return NoContent();
    }

    /// <summary>
    /// Get all relationship types
    /// </summary>
    [HttpGet("types")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RelationshipType>>> GetRelationshipTypes([FromQuery] string? language = null)
    {
        var types = await _relationshipService.GetRelationshipTypes(language);
        return Ok(types);
    }

    /// <summary>
    /// Add a custom relationship type
    /// </summary>
    [HttpPost("types")]
    [Authorize]
    public async Task<IActionResult> AddRelationshipType([FromBody] RelationshipType relType)
    {
        await _relationshipService.AddRelationshipType(
            relType.Type, 
            relType.Language, 
            relType.DisplayName, 
            relType.InverseType, 
            relType.Category);
        return Ok();
    }
}
