using System.Collections.Generic;
using Cassandra;
using ISession = Cassandra.ISession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Coflnet.Connections.Services;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ISession _session;
    private readonly ILogger<AdminController> _logger;
    private readonly DatabaseInitializationService _dbInitService;

    public AdminController(
        ISession session, 
        ILogger<AdminController> logger,
        DatabaseInitializationService dbInitService)
    {
        _session = session;
        _logger = logger;
        _dbInitService = dbInitService;
    }

    /// <summary>
    /// List tables present in the current keyspace (debugging endpoint).
    /// </summary>
    [HttpGet("list-tables")]
    [Authorize]
    public ActionResult<object> ListTables()
    {
        var keyspace = _session?.Keyspace;
        if (string.IsNullOrEmpty(keyspace))
        {
            return BadRequest(new { Success = false, Error = "Session has no keyspace" });
        }

        try
        {
            var rs = _session.Execute(new SimpleStatement("SELECT table_name FROM system_schema.tables WHERE keyspace_name = ?", keyspace));
            var tables = rs.Select(r => r.GetValue<string>("table_name")).ToList();
            return Ok(new { Success = true, Keyspace = keyspace, Tables = tables });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list system tables");
            return StatusCode(500, new { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Drop all application tables so migrations can recreate them from scratch.
    /// WARNING: destructive operation. Restricted to authorized callers.
    /// </summary>
    [HttpPost("drop-all-tables")]
    [Authorize]
    public async Task<ActionResult<object>> DropAllTables()
    {
        var (success, dropped, failed) = await _dbInitService.DropAllTablesAsync();
        return Ok(new { Success = success, Dropped = dropped, Failed = failed });
    }

    /// <summary>
    /// Initialize the database by creating all tables.
    /// Can be called on startup or manually to recreate schema.
    /// </summary>
    [HttpPost("initialize-database")]
    [Authorize]
    public async Task<ActionResult<object>> InitializeDatabase([FromQuery] bool force = false)
    {
        var (success, message) = await _dbInitService.InitializeDatabaseAsync(force);
        return Ok(new { Success = success, Message = message });
    }

    /// <summary>
    /// Test the database by inserting and retrieving sample data from each table.
    /// </summary>
    [HttpPost("test-database")]
    [Authorize]
    public async Task<ActionResult<object>> TestDatabase()
    {
        var (success, results) = await _dbInitService.TestDatabaseAsync();
        return Ok(new { Success = success, Results = results });
    }

    /// <summary>
    /// Full initialization cycle: drop all tables, recreate them, and test.
    /// WARNING: This will delete all data!
    /// </summary>
    [HttpPost("full-initialization-cycle")]
    [Authorize]
    public async Task<ActionResult<object>> FullInitializationCycle()
    {
        var (success, message, details) = await _dbInitService.FullInitializationCycleAsync();
        return Ok(new { Success = success, Message = message, Details = details });
    }

    /// <summary>
    /// Check if the database has been initialized.
    /// </summary>
    [HttpGet("initialization-status")]
    public ActionResult<object> GetInitializationStatus()
    {
        return Ok(new { Initialized = _dbInitService.IsInitialized });
    }
}
