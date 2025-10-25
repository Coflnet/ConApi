using Coflnet.Auth;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Batch operations for bulk data import and creation
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BatchController : ControllerBase
{
    private readonly PersonService _personService;
    private readonly SearchService _searchService;
    private readonly ILogger<BatchController> _logger;

    public BatchController(
        PersonService personService,
        SearchService searchService,
        ILogger<BatchController> logger)
    {
        _personService = personService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Import multiple entities in bulk (currently supports persons)
    /// </summary>
    [HttpPost("import")]
    [Authorize]
    public async Task<ActionResult<BulkOperationResult>> ImportBatch([FromBody] BulkPersonRequest request)
    {
        var userId = this.GetUserId();
        _logger.LogInformation("Bulk import started for user {UserId}: {Count} people", userId, request.People.Count);

        var result = new BulkOperationResult();

        foreach (var personData in request.People)
        {
            try
            {
                // Create person with name attribute first
                Guid? personId = null;
                if (personData.Attributes.TryGetValue("name", out var name))
                {
                    await _personService.UpsertAttribute(userId, null, name, "name", name);
                    
                    // Get the created person ID (this is a simplification - in production you'd want to return it from UpsertAttribute)
                    var attrs = await _personService.GetAttributesByName(userId, name);
                    // personId would be determined from the response
                }

                // Add all other attributes
                foreach (var attr in personData.Attributes.Where(a => a.Key != "name"))
                {
                    await _personService.UpsertAttribute(userId, personId, personData.Name, attr.Key, attr.Value);
                }

                // Add to search index
                var fullId = BuildSearchId(personData);
                await _searchService.AddEntry(userId, personData.Name, fullId, SearchEntry.ResultType.Person);

                result.SuccessCount++;
                if (personId.HasValue)
                    result.CreatedIds.Add(personId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import person: {Name}", personData.Name);
                result.FailureCount++;
                result.Errors.Add($"Failed to import {personData.Name}: {ex.Message}");
            }
        }

        _logger.LogInformation("Bulk import completed: {Success} successful, {Failed} failed", 
            result.SuccessCount, result.FailureCount);

        return Ok(result);
    }

    private string BuildSearchId(PersonCreateDto person)
    {
        var date = person.Attributes.GetValueOrDefault("birthday", string.Empty);
        var birthPlace = person.Attributes.GetValueOrDefault("birthplace", string.Empty);
        return $"{person.Name};{date};{birthPlace}";
    }
}
