using Coflnet.Connections.DTOs;
using Coflnet.Connections.Models;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// API for document and file management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(DocumentService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    /// <summary>
    /// Get a presigned URL for uploading a document
    /// </summary>
    [HttpPost("upload-url")]
    public async Task<ActionResult<PresignedUrlDto>> GetUploadUrl([FromQuery] string fileName, [FromQuery] string? contentType = null)
    {
        var userId = GetUserId();

        var result = await _documentService.GetUploadUrl(userId, fileName, contentType ?? "application/octet-stream");

        if (result == null)
        {
            return BadRequest("Failed to generate upload URL. S3 may not be configured.");
        }

        _logger.LogInformation("Generated upload URL for user {UserId}, file {FileName}", userId, fileName);

        return Ok(result);
    }

    /// <summary>
    /// Create document metadata after successful upload
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Document>> CreateDocument([FromBody] UploadDocumentDto dto)
    {
        var userId = GetUserId();

        var document = await _documentService.CreateDocument(
            userId,
            dto.FileName,
            dto.StorageKey,
            dto.FileSizeBytes,
            dto);

        if (document == null)
        {
            return BadRequest("Failed to create document. Quota may be exceeded.");
        }

        _logger.LogInformation("User {UserId} created document {DocumentId}: {FileName}",
            userId, document.Id, dto.FileName);

        return Ok(document);
    }

    /// <summary>
    /// Get a presigned URL for downloading a document
    /// </summary>
    [HttpGet("{documentId}/download-url")]
    public async Task<ActionResult<PresignedUrlDto>> GetDownloadUrl(Guid documentId)
    {
        var userId = GetUserId();

        var result = await _documentService.GetDownloadUrl(userId, documentId);

        if (result == null)
        {
            return NotFound("Document not found or S3 not configured");
        }

        _logger.LogInformation("Generated download URL for user {UserId}, document {DocumentId}", userId, documentId);

        return Ok(result);
    }

    /// <summary>
    /// Link a document to an entity (person, place, thing, event)
    /// </summary>
    [HttpPost("link")]
    public async Task<IActionResult> LinkDocumentToEntity([FromBody] LinkDocumentDto dto)
    {
        var userId = GetUserId();

        var success = await _documentService.LinkDocumentToEntity(userId, dto);

        if (!success)
        {
            return BadRequest("Failed to link document to entity");
        }

        _logger.LogInformation("User {UserId} linked document {DocumentId} to entity {EntityId}",
            userId, dto.DocumentId, dto.EntityId);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Get all documents linked to an entity
    /// </summary>
    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<IEnumerable<Document>>> GetDocumentsByEntity(Guid entityId)
    {
        var userId = GetUserId();

        var documents = await _documentService.GetDocumentsByEntity(userId, entityId);

        return Ok(documents);
    }

    /// <summary>
    /// Get storage quota information
    /// </summary>
    [HttpGet("quota")]
    public async Task<ActionResult<StorageQuota>> GetStorageQuota()
    {
        var userId = GetUserId();

        var quota = await _documentService.GetStorageQuota(userId);

        return Ok(quota);
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    [HttpDelete("{documentId}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        var userId = GetUserId();

        var success = await _documentService.DeleteDocument(userId, documentId);

        if (!success)
        {
            return NotFound("Document not found or deletion failed");
        }

        _logger.LogInformation("User {UserId} deleted document {DocumentId}", userId, documentId);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Get document by ID
    /// </summary>
    [HttpGet("{documentId}")]
    public async Task<ActionResult<Document>> GetDocument(Guid documentId)
    {
        var userId = GetUserId();

        var document = await _documentService.GetDocument(userId, documentId);

        if (document == null)
        {
            return NotFound();
        }

        return Ok(document);
    }
}
