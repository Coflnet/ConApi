using Coflnet.Connections.DTOs;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Enhanced search API with pagination and advanced filtering
/// </summary>
[ApiController]
[Route("api/v2/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly EnhancedSearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(EnhancedSearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    /// <summary>
    /// Advanced search with pagination and filtering
    /// </summary>
    [HttpPost("advanced")]
    public async Task<ActionResult<SearchResultPage>> SearchAdvanced([FromBody] AdvancedSearchRequest request)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        var results = await _searchService.SearchAdvanced(userId, request);

        return Ok(results);
    }

    /// <summary>
    /// Relational search (e.g., "John's Uncle")
    /// </summary>
    [HttpGet("relational")]
    public async Task<ActionResult<IEnumerable<SearchResult>>> SearchRelational([FromQuery] string query)
    {
        var userId = GetUserId();

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query is required");
        }

        var results = await _searchService.RelationalSearch(userId, query);

        return Ok(results);
    }

    /// <summary>
    /// Search by date range
    /// </summary>
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<SearchResult>>> SearchByDateRange(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] SearchEntry.ResultType? entityType = null)
    {
        var userId = GetUserId();

        var results = await _searchService.SearchByDateRange(userId, startDate, endDate, entityType);

        return Ok(results);
    }
}
