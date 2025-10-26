using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;
using Microsoft.Extensions.Logging;

namespace Coflnet.Connections.Services;

public class MigrationRunner
{
    private readonly ISession _session;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly IServiceProvider _services;

    public MigrationRunner(ISession session, ILogger<MigrationRunner> logger, IServiceProvider services)
    {
        _session = session;
        _logger = logger;
        _services = services;
    }

    /// <summary>
    /// Run all pending migrations sequentially. Uses a small migrations table to persist last applied migration.
    /// </summary>
    public void RunMigrations()
    {
        try
        {
            if (_session == null) {
                _logger.LogWarning("No Cassandra session available - skipping migrations");
                return;
            }

            // Ensure migrations table exists
            var keyspace = _session.Keyspace ?? throw new InvalidOperationException("Session has no keyspace");
            var migrationsTable = $"{keyspace}.migrations";
            var create = $"CREATE TABLE IF NOT EXISTS {migrationsTable} (id text PRIMARY KEY, applied_at timestamp, note text)";
            _session.Execute(new global::Cassandra.SimpleStatement(create));

            // read last applied migration
            var select = new global::Cassandra.SimpleStatement(
                "SELECT note FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?",
                keyspace, "migrations");
            // note: just to ensure table presence in system schema - proceed

            // Determine last applied migration from migrations table
            var last = GetLastAppliedMigration();
            var migrations = BuildMigrationList();

            foreach (var m in migrations)
            {
                if (string.Compare(m.Name, last, StringComparison.Ordinal) <= 0)
                {
                    _logger.LogDebug("Skipping migration {Migration} already applied", m.Name);
                    continue;
                }

                _logger.LogInformation("Running migration {Migration}", m.Name);
                try
                {
                    m.Action();
                    RecordMigration(m.Name, m.Description);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Migration {Migration} failed", m.Name);
                    throw;
                }
            }
            _logger.LogInformation("All migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MigrationRunner failed");
        }
    }

    private string GetLastAppliedMigration()
    {
        try
        {
            var rs = _session.Execute(new global::Cassandra.SimpleStatement($"SELECT note FROM {_session.Keyspace}.migrations WHERE id = 'last'"));
            var row = rs.FirstOrDefault();
            if (row != null)
            {
                return row.GetValue<string>("note") ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }

    private void RecordMigration(string name, string? note)
    {
        try
        {
            var c = new global::Cassandra.SimpleStatement($"INSERT INTO {_session.Keyspace}.migrations (id, applied_at, note) VALUES ('last', toTimestamp(now()), ?)", name + ":" + (note ?? ""));
            _session.Execute(c);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record migration {Migration}", name);
        }
    }

        private List<(string Name, Action Action, string Description)> BuildMigrationList()
    {
        var list = new List<(string, Action, string)>();

        // Migration 001: Ensure schemas for services
        list.Add(("001_ensure_schemas", () =>
        {
            // Resolve known services from the provider and invoke EnsureSchema if present
            var known = new[] { 
                typeof(SearchService), 
                typeof(PersonService), 
                typeof(PlaceService), 
                typeof(ThingService), 
                typeof(EventService), 
                typeof(RelationshipService),
                typeof(ShareService),
                typeof(DocumentService),
                typeof(SourceCitationService)
            };
            foreach (var kt in known)
            {
                try
                {
                    var s = _services.GetService(kt);
                    if (s == null) continue;
                    var t = s.GetType();
                    var m = t.GetMethod("EnsureSchema");
                    if (m != null)
                    {
                        _logger.LogInformation("Applying schema step for {Service}", t.Name);
                        m.Invoke(s, null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed schema step for {Service}", kt.Name);
                }
            }

            // After creating tables, wait for metadata to propagate for common application tables
            try
            {
                var tables = new[] {
                    "person","person_data","place","place_data","thing","thing_data","thing_by_owner",
                    "event","event_data","relationship","relationship_by_from","relationship_by_to","relationship_type",
                    "search_entry","share_invitation","share_invitation_by_recipient","data_provenance","data_conflict",
                    "document","document_link","document_by_entity","storage_quota","source_citation","citation_by_source","conflicting_information"
                };
                SchemaHelper.WaitForTables(_session, _logger, tables);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WaitForTables failed");
            }
        }, "Create tables and set LCS where needed"));

        return list;
    }
}
