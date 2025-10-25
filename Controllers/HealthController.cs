using Microsoft.AspNetCore.Mvc;

namespace Coflnet.Connections.Controllers;

/// <summary>
/// Health and status endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Basic health check
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "2.0.0"
        });
    }

    /// <summary>
    /// Detailed status information
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "2.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            MachineName = Environment.MachineName,
            ProcessId = Environment.ProcessId,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        });
    }
}
