using System;
using System.Collections.Generic;
using System.Linq;
using Cassandra;
using Microsoft.Extensions.Logging;

namespace Coflnet.Connections.Services;

public static class SchemaHelper
{
    /// <summary>
    /// Ensure the target table uses LeveledCompactionStrategy. This helper will try common
    /// table-name variants (with and without underscores) when checking system_schema so
    /// services that use different naming conventions won't miss the table.
    /// Also waits with exponential backoff to ensure table visibility before returning.
    /// </summary>
    public static void TryEnsureLcs(global::Cassandra.ISession session, ILogger? logger, string tableName)
    {
        try
        {
            var keyspace = session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                if (logger != null) logger.LogDebug("Session has no keyspace; skipping LCS check for {Table}", tableName);
                else Console.WriteLine($"Session has no keyspace; skipping LCS check for {tableName}");
                return;
            }

            if (session == null)
            {
                if (logger != null) logger.LogDebug("No session available when checking compaction for {Table}", tableName);
                else Console.WriteLine($"No session available when checking compaction for {tableName}");
                return;
            }

            // Candidate table names: provided and without underscores
            var candidate1 = tableName;
            var candidate2 = tableName.Replace("_", string.Empty);
            var cql = "SELECT compaction, table_name FROM system_schema.tables WHERE keyspace_name = ? AND table_name IN (?, ?)";
            var select = new global::Cassandra.SimpleStatement(cql, keyspace, candidate1, candidate2);

            global::Cassandra.Row? row = null;
            // Wait with exponential backoff: 100ms, 200ms, 500ms, 1000ms, 2000ms, 5000ms (up to ~9 seconds total)
            int[] delays = { 100, 200, 500, 1000, 2000, 5000 };
            for (int i = 0; i < delays.Length; i++)
            {
                var rs = session.Execute(select);
                row = rs.FirstOrDefault();
                if (row != null) break;
                System.Threading.Thread.Sleep(delays[i]);
            }

            if (row == null)
            {
                if (logger != null) logger.LogDebug("Table {Table} not yet visible in system_schema (candidates: {C1},{C2}); skipping LCS", tableName, candidate1, candidate2);
                else Console.WriteLine($"Table {tableName} not yet visible in system_schema; skipping LCS");
                return;
            }

            // Determine which actual table name we matched (use returned table_name)
            string actualName = tableName;
            try { actualName = row.GetValue<string>("table_name"); } catch { }

            try
            {
                var compaction = row.GetValue<IDictionary<string, string>>("compaction");
                if (compaction != null && compaction.TryGetValue("class", out var cls) && !string.IsNullOrEmpty(cls)
                    && cls.Contains("LeveledCompactionStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    if (logger != null) logger.LogDebug("Table {Table} already uses compaction {Class}", actualName, cls);
                    else Console.WriteLine($"Table {actualName} already uses compaction {cls}");
                    return;
                }
            }
            catch { }

            var target = $"{keyspace}.{actualName}";
            var alter = $"ALTER TABLE {target} WITH compaction = {{'class':'LeveledCompactionStrategy'}}";
            session.Execute(new global::Cassandra.SimpleStatement(alter));
        }
        catch (Exception ex)
        {
            if (logger != null) logger.LogDebug(ex, "EnsureLcs failed for {Table}", tableName);
            else Console.WriteLine($"EnsureLcs failed for {tableName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensure the specified column exists on the given table. If the column is missing this will
    /// execute an ALTER TABLE ADD ... statement with the provided CQL type (for example: "map&lt;text,text&gt;").
    /// Uses the same candidate table-name logic and exponential backoff as TryEnsureLcs to account for
    /// metadata propagation delays in Cassandra clusters.
    /// </summary>
    public static void TryEnsureColumn(global::Cassandra.ISession session, ILogger? logger, string tableName, string columnName, string cqlType)
    {
        try
        {
            var keyspace = session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                if (logger != null) logger.LogDebug("Session has no keyspace; skipping column check for {Table}.{Column}", tableName, columnName);
                else Console.WriteLine($"Session has no keyspace; skipping column check for {tableName}.{columnName}");
                return;
            }

            if (session == null)
            {
                if (logger != null) logger.LogDebug("No session available when checking column for {Table}.{Column}", tableName, columnName);
                else Console.WriteLine($"No session available when checking column for {tableName}.{columnName}");
                return;
            }

            var candidate1 = tableName;
            var candidate2 = tableName.Replace("_", string.Empty);
            var cql = "SELECT column_name, table_name FROM system_schema.columns WHERE keyspace_name = ? AND table_name IN (?, ?) AND column_name = ?";
            var select = new global::Cassandra.SimpleStatement(cql, keyspace, candidate1, candidate2, columnName);

            global::Cassandra.Row? row = null;
            int[] delays = { 100, 200, 500, 1000, 2000, 5000 };
            for (int i = 0; i < delays.Length; i++)
            {
                var rs = session.Execute(select);
                row = rs.FirstOrDefault();
                if (row != null) break;
                System.Threading.Thread.Sleep(delays[i]);
            }

            if (row != null)
            {
                if (logger != null) logger.LogDebug("Column {Column} already exists on {Table}", columnName, row.GetValue<string>("table_name"));
                else Console.WriteLine($"Column {columnName} already exists on table {tableName}");
                return;
            }

            // Not visible yet; attempt to ALTER TABLE to add column. Use actual table name candidate1 by default.
            var target = $"{keyspace}.{candidate1}";
            var alter = $"ALTER TABLE {target} ADD {columnName} {cqlType}";
            try
            {
                session.Execute(new global::Cassandra.SimpleStatement(alter));
                if (logger != null) logger.LogInformation("Added column {Column} to {Table} as {Type}", columnName, target, cqlType);
                else Console.WriteLine($"Added column {columnName} to {target} as {cqlType}");

                // wait briefly for metadata propagation and verify
                for (int i = 0; i < delays.Length; i++)
                {
                    var rs = session.Execute(select);
                    row = rs.FirstOrDefault();
                    if (row != null) break;
                    System.Threading.Thread.Sleep(delays[i]);
                }

                if (row == null)
                {
                    if (logger != null) logger.LogWarning("Column {Column} added but not yet visible in system_schema for {Table}", columnName, tableName);
                    else Console.WriteLine($"Column {columnName} added but not yet visible in system_schema for {tableName}");
                }
            }
            catch (Exception ex)
            {
                if (logger != null) logger.LogWarning(ex, "Failed to add column {Column} to {Table}", columnName, target);
                else Console.WriteLine($"Failed to add column {columnName} to {target}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            if (logger != null) logger.LogDebug(ex, "TryEnsureColumn failed for {Table}.{Column}", tableName, columnName);
            else Console.WriteLine($"TryEnsureColumn failed for {tableName}.{columnName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Wait for the provided application table names to appear in system_schema.tables.
    /// This is intended to be used after CREATE TABLE/ALTER TABLE operations to ensure
    /// metadata has propagated across the cluster before the driver attempts to prepare
    /// statements against those tables.
    /// </summary>
    public static void WaitForTables(global::Cassandra.ISession session, ILogger? logger, IEnumerable<string> tableNames)
    {
        try
        {
            var keyspace = session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                if (logger != null) logger.LogDebug("Session has no keyspace; skipping WaitForTables");
                else Console.WriteLine("Session has no keyspace; skipping WaitForTables");
                return;
            }

            if (session == null) return;

            var names = tableNames.ToList();
            if (!names.Any()) return;

            // Build a single SELECT that checks for presence of any of the candidate names
            var candidateList = string.Join(',', names.Select(n => "'" + n + "'"));

            // We'll poll system_schema.tables with exponential backoff and also try each name with and without underscores.
            int[] delays = { 100, 200, 500, 1000, 2000, 5000 };
            var remaining = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

            for (int attempt = 0; attempt < delays.Length && remaining.Count > 0; attempt++)
            {
                try
                {
                    // Ask the driver to refresh metadata from the cluster to speed up visibility
                    try { session.Cluster.RefreshSchema(); } catch { }

                    // Query system schema for all tables in keyspace
                    var rs = session.Execute(new global::Cassandra.SimpleStatement("SELECT table_name FROM system_schema.tables WHERE keyspace_name = ?", keyspace));
                    var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in rs)
                    {
                        try { present.Add(r.GetValue<string>("table_name")); } catch { }
                    }

                    // Check which requested tables are present (allowing underscore/no-underscore variants)
                    var found = new List<string>();
                    foreach (var name in remaining)
                    {
                        var alt = name.Replace("_", string.Empty);
                        if (present.Contains(name) || present.Contains(alt))
                        {
                            found.Add(name);
                        }
                    }

                    foreach (var f in found) remaining.Remove(f);
                    if (remaining.Count == 0) break;
                }
                catch (Exception ex)
                {
                    if (logger != null) logger.LogDebug(ex, "WaitForTables attempt failed");
                }

                try { session.Cluster.RefreshSchema(); } catch { }
                System.Threading.Thread.Sleep(delays[attempt]);
            }

            if (remaining.Count > 0)
            {
                if (logger != null) logger.LogWarning("Some tables did not become visible in system_schema: {Tables}", string.Join(',', remaining));
                else Console.WriteLine("Some tables did not become visible: " + string.Join(',', remaining));
            }
            else
            {
                if (logger != null) logger.LogDebug("All requested tables visible in system_schema");
            }
        }
        catch (Exception ex)
        {
            if (logger != null) logger.LogDebug(ex, "WaitForTables failed");
            else Console.WriteLine($"WaitForTables failed: {ex.Message}");
        }
    }
}
