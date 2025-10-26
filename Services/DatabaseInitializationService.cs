using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Centralized database initialization service that handles table creation, dropping, and validation.
/// This service runs on startup and can be triggered via admin endpoint.
/// </summary>
public class DatabaseInitializationService
{
    private readonly ISession _session;
    private readonly ILogger<DatabaseInitializationService> _logger;
    private readonly IServiceProvider _services;
    private bool _isInitialized = false;
    private readonly object _initLock = new object();

    /// <summary>
    /// Creates a new DatabaseInitializationService.
    /// </summary>
    public DatabaseInitializationService(
        ISession session, 
        ILogger<DatabaseInitializationService> logger, 
        IServiceProvider services)
    {
        _session = session;
        _logger = logger;
        _services = services;
    }

    /// <summary>
    /// Whether the database initialization has completed successfully in this process.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Get all application table names.
    /// </summary>
    private string[] GetAllTableNames()
    {
        return new[]
        {
            "person2", // Note: person table is named person2 in mapping
            "person_data",
            "place",
            "place_data",
            "thing",
            "thing_data",
            "thing_by_owner",
            "event",
            "event_data",
            "relationship",
            "relationship_by_from",
            "relationship_by_to",
            "relationship_type",
            "search_entry",
            "share_invitation",
            "share_invitation_by_recipient",
            "data_provenance",
            "data_conflict",
            "document",
            "document_link",
            "document_by_entity",
            "storage_quota",
            "source_citation",
            "citation_by_source",
            "conflicting_information"
        };
    }

    /// <summary>
    /// Drop all application tables.
    /// </summary>
    public async Task<(bool Success, List<string> Dropped, List<string> Failed)> DropAllTablesAsync()
    {
        var session = _session;
        if (session == null)
        {
            _logger.LogError("No Cassandra session available; cannot drop tables");
            return (false, new List<string>(), new List<string> { "No session" });
        }

        var keyspace = session.Keyspace;
        if (string.IsNullOrEmpty(keyspace))
        {
            _logger.LogError("Session has no keyspace; cannot drop tables");
            return (false, new List<string>(), new List<string> { "No keyspace" });
        }

        var tables = GetAllTableNames();
        var dropped = new List<string>();
        var failed = new List<string>();

        foreach (var table in tables)
        {
            try
            {
                var cql = $"DROP TABLE IF EXISTS {keyspace}.{table}";
                await session.ExecuteAsync(new SimpleStatement(cql));
                _logger.LogInformation("Dropped table {Table}", table);
                dropped.Add(table);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to drop table {Table}", table);
                failed.Add($"{table}: {ex.Message}");
            }
        }

        _isInitialized = false;
        return (failed.Count == 0, dropped, failed);
    }

    /// <summary>
    /// Initialize database by creating all tables.
    /// </summary>
    public async Task<(bool Success, string Message)> InitializeDatabaseAsync(bool force = false)
    {
        lock (_initLock)
        {
            if (_isInitialized && !force)
            {
                _logger.LogInformation("Database already initialized, skipping");
                return (true, "Database already initialized");
            }
        }

        var session = _session;
        if (session == null)
        {
            _logger.LogError("No Cassandra session available; cannot initialize database");
            return (false, "No session");
        }

        try
        {
            _logger.LogInformation("Starting database initialization...");

            // Force initialization of centralized mapping configuration
            _ = GlobalMapping.Instance;

            // Create all tables by calling EnsureSchema on all services
            var services = new[]
            {
                (typeof(SearchService), "SearchService"),
                (typeof(PersonService), "PersonService"),
                (typeof(PlaceService), "PlaceService"),
                (typeof(ThingService), "ThingService"),
                (typeof(EventService), "EventService"),
                (typeof(RelationshipService), "RelationshipService"),
                (typeof(ShareService), "ShareService"),
                (typeof(DocumentService), "DocumentService"),
                (typeof(SourceCitationService), "SourceCitationService")
            };

            foreach (var (serviceType, serviceName) in services)
            {
                try
                {
                    var service = _services.GetService(serviceType);
                    if (service == null)
                    {
                        _logger.LogWarning("Service {ServiceName} not found in DI container", serviceName);
                        continue;
                    }

                    var method = serviceType.GetMethod("EnsureSchema");
                    if (method != null)
                    {
                        _logger.LogInformation("Creating schema for {ServiceName}", serviceName);
                        method.Invoke(service, null);
                    }
                    else
                    {
                        _logger.LogWarning("Service {ServiceName} does not have EnsureSchema method", serviceName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create schema for {ServiceName}", serviceName);
                    return (false, $"Failed to create schema for {serviceName}: {ex.Message}");
                }
            }

            // Wait for all tables to be visible in system schema
            _logger.LogInformation("Waiting for tables to be visible in system schema...");
            SchemaHelper.WaitForTables(session, _logger, GetAllTableNames());

            // Force driver metadata refresh and optionally pre-prepare a trivial query for each table
            try
            {
                _logger.LogInformation("Refreshing driver schema metadata to avoid prepare-time races...");
                try { session.Cluster.RefreshSchema(); } catch { }

                // Pre-prepare a simple SELECT for each table so the driver creates prepared statements now
                foreach (var table in GetAllTableNames())
                {
                    try
                    {
                        var keyspace = session.Keyspace;
                        if (string.IsNullOrEmpty(keyspace)) continue;
                        // Try both underscored and concatenated variants to match actual name
                        var candidates = new[] { table, table.Replace("_", string.Empty) };
                        foreach (var t in candidates)
                        {
                            try
                            {
                                var cql = $"SELECT * FROM {keyspace}.\"{t}\" LIMIT 1";
                                // Use SimpleStatement to let the driver prepare it if needed
                                var st = new SimpleStatement(cql);
                                // Execute synchronously to force prepare; timeouts are caught below
                                session.Execute(st);
                                break; // if one candidate succeeded, stop trying others
                            }
                            catch
                            {
                                // ignore and try next candidate
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Pre-prepare select failed for table {Table}", table);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Driver schema refresh / pre-prepare step failed");
            }

            _logger.LogInformation("Database initialization completed successfully");
            _isInitialized = true;
            return (true, "Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            return (false, $"Database initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Test database by verifying all tables exist in system schema.
    /// </summary>
    public async Task<(bool Success, Dictionary<string, string> Results)> TestDatabaseAsync()
    {
        var results = new Dictionary<string, string>();
        var tables = GetAllTableNames();

        try
        {
            var session = _session;
            if (session == null)
            {
                results["_error"] = "No session available";
                return (false, results);
            }

            var keyspace = session.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                results["_error"] = "No keyspace configured";
                return (false, results);
            }

            // Check each table exists in system schema (accept variants: with and without underscore)
            foreach (var table in tables)
            {
                try
                {
                    var alt = table.Replace("_", string.Empty);
                    var cql = "SELECT table_name FROM system_schema.tables WHERE keyspace_name = ? AND table_name IN (?, ?)";
                    var rs = await session.ExecuteAsync(new SimpleStatement(cql, keyspace, table, alt));
                    var row = rs.FirstOrDefault();

                    if (row != null)
                    {
                        var actual = string.Empty;
                        try { actual = row.GetValue<string>("table_name") ?? string.Empty; } catch { }
                        results[table] = $"OK - Table exists as {actual}";
                        _logger.LogInformation("Test passed: table {Requested} exists as {Actual}", table, actual);
                    }
                    else
                    {
                        results[table] = "FAILED - Table not found";
                        _logger.LogError("Test failed: table {TableName} not found in system schema", table);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Test failed for table {TableName}", table);
                    results[table] = $"FAILED: {ex.Message}";
                }
            }

            var allSucceeded = results.Values.All(v => v.StartsWith("OK"));
            return (allSucceeded, results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database test failed");
            results["_error"] = ex.Message;
            return (false, results);
        }
    }

    /// <summary>
    /// Full initialization cycle: drop all tables, recreate them, and test.
    /// </summary>
    public async Task<(bool Success, string Message, Dictionary<string, object> Details)> FullInitializationCycleAsync()
    {
        var details = new Dictionary<string, object>();

        try
        {
            // Step 1: Drop all tables
            _logger.LogInformation("Step 1: Dropping all tables...");
            var (dropSuccess, dropped, failed) = await DropAllTablesAsync();
            details["dropped_tables"] = dropped;
            details["drop_failures"] = failed;

            if (!dropSuccess)
            {
                return (false, "Failed to drop all tables", details);
            }

            // Wait a bit for schema changes to propagate
            await Task.Delay(2000);

            // Step 2: Create all tables
            _logger.LogInformation("Step 2: Creating all tables...");
            var (initSuccess, initMessage) = await InitializeDatabaseAsync(force: true);
            details["initialization_message"] = initMessage;

            if (!initSuccess)
            {
                return (false, "Failed to initialize database", details);
            }

            // Wait a bit for schema to be ready
            await Task.Delay(2000);

            // Step 3: Test all tables
            _logger.LogInformation("Step 3: Testing all tables...");
            var (testSuccess, testResults) = await TestDatabaseAsync();
            details["test_results"] = testResults;

            if (!testSuccess)
            {
                return (false, "Database tests failed", details);
            }

            return (true, "Full initialization cycle completed successfully", details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full initialization cycle failed");
            details["error"] = ex.Message;
            return (false, $"Full initialization cycle failed: {ex.Message}", details);
        }
    }
}
