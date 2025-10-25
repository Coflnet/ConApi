using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing source citations and conflicting information
/// </summary>
public class SourceCitationService
{
    private readonly ISession _session;
    private readonly Table<SourceCitation> _citations;
    private readonly Table<CitationBySource> _citationsBySource;
    private readonly Table<ConflictingInformation> _conflicts;
    private readonly ILogger<SourceCitationService> _logger;

    public SourceCitationService(ISession session, ILogger<SourceCitationService> logger)
    {
        _session = session;
        _logger = logger;

        var citationMapping = new MappingConfiguration()
            .Define(new Map<SourceCitation>()
                .PartitionKey(c => c.UserId)
                .ClusteringKey(c => c.EntityId)
                .ClusteringKey(c => c.FieldName)
                .Column(c => c.EntityType, col => col.WithName("entity_type").WithDbType<int>())
                .Column(c => c.SourceType, col => col.WithName("source_type").WithDbType<int>())
                .Column(c => c.PrivacyLevel, col => col.WithName("privacy_level").WithDbType<int>()));

        var bySourceMapping = new MappingConfiguration()
            .Define(new Map<CitationBySource>()
                .PartitionKey(c => c.UserId)
                .ClusteringKey(c => c.Title)
                .ClusteringKey(c => c.CitationId)
                .Column(c => c.EntityType, col => col.WithName("entity_type").WithDbType<int>()));

        var conflictMapping = new MappingConfiguration()
            .Define(new Map<ConflictingInformation>()
                .PartitionKey(c => c.UserId)
                .ClusteringKey(c => c.EntityId)
                .ClusteringKey(c => c.FieldName)
                .Column(c => c.Resolution, col => col.WithName("resolution").WithDbType<int>())
                .Column(c => c.PrivacyLevel, col => col.WithName("privacy_level").WithDbType<int>()));

        _citations = new Table<SourceCitation>(session, citationMapping);
        _citationsBySource = new Table<CitationBySource>(session, bySourceMapping);
        _conflicts = new Table<ConflictingInformation>(session, conflictMapping);
    }

    /// <summary>
    /// Add a source citation
    /// </summary>
    public async Task<SourceCitation> AddCitation(Guid userId, SourceCitation citation)
    {
        citation.UserId = userId;
        citation.Id = Guid.NewGuid();
        citation.CreatedAt = DateTime.UtcNow;
        citation.UpdatedAt = DateTime.UtcNow;

        await _citations.Insert(citation).ExecuteAsync();

        // Add denormalized entry
        var bySource = new CitationBySource
        {
            UserId = userId,
            Title = citation.Title,
            CitationId = citation.Id,
            EntityId = citation.EntityId,
            EntityType = citation.EntityType,
            FieldName = citation.FieldName,
            QualityRating = citation.QualityRating,
            CreatedAt = citation.CreatedAt
        };

        await _citationsBySource.Insert(bySource).ExecuteAsync();

        _logger.LogInformation("Added citation {CitationId} for entity {EntityId}, field {FieldName}",
            citation.Id, citation.EntityId, citation.FieldName);

        return citation;
    }

    /// <summary>
    /// Get citations for an entity field
    /// </summary>
    public async Task<IEnumerable<SourceCitation>> GetCitations(Guid userId, Guid entityId, string? fieldName = null)
    {
        var query = _citations.Where(c => c.UserId == userId && c.EntityId == entityId);

        if (!string.IsNullOrEmpty(fieldName))
        {
            query = query.Where(c => c.FieldName == fieldName);
        }

        return await query.ExecuteAsync();
    }

    /// <summary>
    /// Get citations by source title
    /// </summary>
    public async Task<IEnumerable<CitationBySource>> GetCitationsBySource(Guid userId, string sourceTitle)
    {
        return await _citationsBySource
            .Where(c => c.UserId == userId && c.Title == sourceTitle)
            .ExecuteAsync();
    }

    /// <summary>
    /// Record conflicting information
    /// </summary>
    public async Task<ConflictingInformation> RecordConflict(
        Guid userId,
        Guid entityId,
        string fieldName,
        string value1,
        string value2,
        Guid? citation1Id = null,
        Guid? citation2Id = null)
    {
        var conflict = new ConflictingInformation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntityId = entityId,
            FieldName = fieldName,
            Value1 = value1,
            Value2 = value2,
            Citation1Id = citation1Id,
            Citation2Id = citation2Id,
            Resolution = ConflictResolutionStrategy.Unresolved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _conflicts.Insert(conflict).ExecuteAsync();

        _logger.LogWarning("Recorded conflict for entity {EntityId}, field {FieldName}: '{Value1}' vs '{Value2}'",
            entityId, fieldName, value1, value2);

        return conflict;
    }

    /// <summary>
    /// Resolve a conflict
    /// </summary>
    public async Task<ConflictingInformation?> ResolveConflict(
        Guid userId,
        Guid entityId,
        string fieldName,
        ConflictResolutionStrategy strategy,
        string? preferredValue = null)
    {
        var conflicts = await _conflicts
            .Where(c => c.UserId == userId && c.EntityId == entityId && c.FieldName == fieldName)
            .ExecuteAsync();

        var conflict = conflicts.FirstOrDefault();
        if (conflict == null)
        {
            return null;
        }

        conflict.Resolution = strategy;
        conflict.PreferredValue = preferredValue;
        conflict.ResolvedAt = DateTime.UtcNow;
        conflict.UpdatedAt = DateTime.UtcNow;

        await _conflicts.Insert(conflict).ExecuteAsync();

        _logger.LogInformation("Resolved conflict for entity {EntityId}, field {FieldName} using strategy {Strategy}",
            entityId, fieldName, strategy);

        return conflict;
    }

    /// <summary>
    /// Get unresolved conflicts for a user
    /// </summary>
    public async Task<IEnumerable<ConflictingInformation>> GetUnresolvedConflicts(Guid userId)
    {
        var allConflicts = await _conflicts
            .Where(c => c.UserId == userId)
            .Take(1000)
            .ExecuteAsync();

        return allConflicts.Where(c => c.Resolution == ConflictResolutionStrategy.Unresolved);
    }

    /// <summary>
    /// Ensure schema exists
    /// </summary>
    public void EnsureSchema()
    {
        _citations.CreateIfNotExists();
        _citationsBySource.CreateIfNotExists();
        _conflicts.CreateIfNotExists();

        TryEnsureLcs("source_citation");
        TryEnsureLcs("citation_by_source");
        TryEnsureLcs("conflicting_information");
    }

    private void TryEnsureLcs(string tableName)
    {
        try
        {
            var keyspace = _session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace) || _session == null)
            {
                _logger.LogDebug("Session has no keyspace; skipping LCS check for {Table}", tableName);
                return;
            }

            var select = new global::Cassandra.SimpleStatement(
                "SELECT compaction FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?",
                keyspace, tableName);

            global::Cassandra.Row? row = null;
            for (int i = 0; i < 6; i++)
            {
                var rs = _session.Execute(select);
                row = rs.FirstOrDefault();
                if (row != null) break;
                System.Threading.Thread.Sleep(500);
            }

            if (row == null)
            {
                _logger.LogDebug("Table {Table} not yet visible in system_schema; skipping LCS", tableName);
                return;
            }

            try
            {
                var compaction = row.GetValue<IDictionary<string, string>>("compaction");
                if (compaction != null && compaction.TryGetValue("class", out var cls) &&
                    !string.IsNullOrEmpty(cls) && cls.Contains("LeveledCompactionStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Table {Table} already uses compaction {Class}", tableName, cls);
                    return;
                }
            }
            catch { }

            var target = $"{keyspace}.{tableName}";
            var cql = $"ALTER TABLE {target} WITH compaction = {{'class':'LeveledCompactionStrategy'}}";
            _session.Execute(new global::Cassandra.SimpleStatement(cql));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to ensure LCS for table {Table}", tableName);
        }
    }
}
