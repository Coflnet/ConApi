using Coflnet.Connections.DTOs;
using Coflnet.Connections.Models;
using Coflnet.Connections.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// API for data sharing and collaboration
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShareController : ControllerBase
{
    private readonly ShareService _shareService;
    private readonly ILogger<ShareController> _logger;

    public ShareController(ShareService shareService, ILogger<ShareController> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    /// <summary>
    /// Create a new share invitation
    /// </summary>
    [HttpPost("invite")]
    public async Task<ActionResult<ShareInvitation>> CreateInvitation([FromBody] CreateShareInvitationDto dto)
    {
        var userId = GetUserId();
        var invitation = await _shareService.CreateInvitation(userId, dto);

        if (invitation == null)
        {
            return BadRequest("Failed to create invitation");
        }

        _logger.LogInformation("User {UserId} created share invitation {InvitationId} for user {ToUserId}",
            userId, invitation.Id, dto.ToUserId);

        return Ok(invitation);
    }

    /// <summary>
    /// Get all sent invitations
    /// </summary>
    [HttpGet("invitations/sent")]
    public async Task<ActionResult<IEnumerable<ShareInvitation>>> GetSentInvitations()
    {
        var userId = GetUserId();
        var invitations = await _shareService.GetInvitationsByUser(userId, sent: true);

        return Ok(invitations);
    }

    /// <summary>
    /// Get all received invitations
    /// </summary>
    [HttpGet("invitations/received")]
    public async Task<ActionResult<IEnumerable<ShareInvitation>>> GetReceivedInvitations([FromQuery] bool? pendingOnly = true)
    {
        var userId = GetUserId();

        if (pendingOnly == true)
        {
            var pending = await _shareService.GetPendingInvitations(userId);
            return Ok(pending);
        }

        var invitations = await _shareService.GetInvitationsByUser(userId, sent: false);
        return Ok(invitations);
    }

    /// <summary>
    /// Respond to a share invitation
    /// </summary>
    [HttpPost("invitations/{invitationId}/respond")]
    public async Task<IActionResult> RespondToInvitation(Guid invitationId, [FromBody] RespondToInvitationDto dto)
    {
        var userId = GetUserId();

        var result = await _shareService.RespondToInvitation(userId, invitationId, dto.Accept, dto.DefaultConflictResolution ?? ConflictResolution.Manual);

        if (result == null)
        {
            return BadRequest("Failed to respond to invitation");
        }

        _logger.LogInformation("User {UserId} {Response} invitation {InvitationId}",
            userId, dto.Accept ? "accepted" : "rejected", invitationId);

        return Ok(new { Success = true, Accepted = dto.Accept });
    }

    /// <summary>
    /// Get change history for an entity
    /// </summary>
    [HttpGet("history/{entityId}")]
    public async Task<ActionResult<IEnumerable<DataProvenance>>> GetChangeHistory(Guid entityId)
    {
        var userId = GetUserId();
        var history = await _shareService.GetChangeHistory(entityId);

        return Ok(history);
    }

    /// <summary>
    /// Get all unresolved conflicts
    /// </summary>
    [HttpGet("conflicts")]
    public async Task<ActionResult<IEnumerable<DataConflict>>> GetUnresolvedConflicts()
    {
        var userId = GetUserId();
        var conflicts = await _shareService.GetUnresolvedConflicts(userId);

        return Ok(conflicts);
    }

    /// <summary>
    /// Resolve a data conflict
    /// </summary>
    [HttpPost("conflicts/resolve")]
    public async Task<IActionResult> ResolveConflict([FromBody] ResolveConflictDto dto)
    {
        var userId = GetUserId();

        var result = await _shareService.ResolveConflict(
            userId,
            dto.EntityId,
            dto.FieldName,
            dto.Resolution);

        if (result == null)
        {
            return BadRequest("Failed to resolve conflict");
        }

        _logger.LogInformation("User {UserId} resolved conflict {ConflictId} with resolution {Resolution}",
            userId, dto.ConflictId, dto.Resolution);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Export user data
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> ExportData([FromBody] ExportRequestDto dto, [FromServices] ExportService exportService)
    {
        var userId = GetUserId();

        var json = await exportService.ExportToJson(userId, dto);

        var fileName = $"connections-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            fileName);
    }

    /// <summary>
    /// Import user data
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> ImportData([FromBody] string jsonData, [FromServices] ExportService exportService)
    {
        var userId = GetUserId();

        var result = await exportService.ImportFromJson(userId, jsonData);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
