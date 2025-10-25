using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// API for managing source citations and conflicting information
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CitationController : ControllerBase
{
    private readonly SourceCitationService _citationService;
    private readonly ILogger<CitationController> _logger;

    public CitationController(SourceCitationService citationService, ILogger<CitationController> logger)
    {
        _citationService = citationService;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    /// <summary>
    /// Add a source citation
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SourceCitation>> AddCitation([FromBody] SourceCitation citation)
    {
        var userId = GetUserId();

        var result = await _citationService.AddCitation(userId, citation);

        return Ok(result);
    }

    /// <summary>
    /// Get citations for an entity
    /// </summary>
    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<SourceCitation>>> GetCitations(
        Guid entityId,
        [FromQuery] string? fieldName = null)
    {
        var userId = GetUserId();

        var citations = await _citationService.GetCitations(userId, entityId, fieldName);

        return Ok(citations);
    }

    /// <summary>
    /// Get citations by source title
    /// </summary>
    [HttpGet("source/{sourceTitle}")]
    public async Task<ActionResult<IEnumerable<CitationBySource>>> GetCitationsBySource(string sourceTitle)
    {
        var userId = GetUserId();

        var citations = await _citationService.GetCitationsBySource(userId, sourceTitle);

        return Ok(citations);
    }

    /// <summary>
    /// Record conflicting information
    /// </summary>
    [HttpPost("conflict")]
    public async Task<ActionResult<ConflictingInformation>> RecordConflict([FromBody] RecordConflictRequest request)
    {
        var userId = GetUserId();

        var conflict = await _citationService.RecordConflict(
            userId,
            request.EntityId,
            request.FieldName,
            request.Value1,
            request.Value2,
            request.Citation1Id,
            request.Citation2Id);

        return Ok(conflict);
    }

    /// <summary>
    /// Resolve a conflict
    /// </summary>
    [HttpPost("conflict/resolve")]
    public async Task<ActionResult<ConflictingInformation>> ResolveConflict([FromBody] ResolveConflictRequest request)
    {
        var userId = GetUserId();

        var result = await _citationService.ResolveConflict(
            userId,
            request.EntityId,
            request.FieldName,
            request.Strategy,
            request.PreferredValue);

        if (result == null)
        {
            return NotFound("Conflict not found");
        }

        return Ok(result);
    }

    /// <summary>
    /// Get unresolved conflicts
    /// </summary>
    [HttpGet("conflicts/unresolved")]
    public async Task<ActionResult<IEnumerable<ConflictingInformation>>> GetUnresolvedConflicts()
    {
        var userId = GetUserId();

        var conflicts = await _citationService.GetUnresolvedConflicts(userId);

        return Ok(conflicts);
    }
}

/// <summary>
/// Request to record a conflict
/// </summary>
public class RecordConflictRequest
{
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string Value1 { get; set; } = string.Empty;
    public string Value2 { get; set; } = string.Empty;
    public Guid? Citation1Id { get; set; }
    public Guid? Citation2Id { get; set; }
}

/// <summary>
/// Request to resolve a conflict
/// </summary>
public class ResolveConflictRequest
{
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public ConflictResolutionStrategy Strategy { get; set; }
    public string? PreferredValue { get; set; }
}
