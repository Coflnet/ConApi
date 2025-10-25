using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Connections.Models;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing bidirectional relationships between entities
/// </summary>
public class RelationshipService
{
    private readonly ISession _session;
    private readonly Table<Relationship> _relationships;
    private readonly Table<RelationshipByFrom> _relationshipsByFrom;
    private readonly Table<RelationshipByTo> _relationshipsByTo;
    private readonly Table<RelationshipType> _relationshipTypes;
    private readonly ILogger<RelationshipService> _logger;

    public RelationshipService(ISession session, ILogger<RelationshipService> logger)
    {
        _session = session;
        _logger = logger;
        
        var relationshipMapping = new MappingConfiguration()
            .Define(new Map<Relationship>()
                .PartitionKey(t => t.UserId, t => t.Id)
                .ClusteringKey(t => t.FromEntityId)
                .ClusteringKey(t => t.ToEntityId)
                .Column(t => t.FromEntityType, c => c.WithName("from_entity_type").WithDbType<int>())
                .Column(t => t.ToEntityType, c => c.WithName("to_entity_type").WithDbType<int>()));
                
        var typeMapping = new MappingConfiguration()
            .Define(new Map<RelationshipType>()
                .PartitionKey(t => t.Type)
                .ClusteringKey(t => t.Language));
        
        _relationships = new Table<Relationship>(session, relationshipMapping);
        var byFromMapping = new MappingConfiguration()
            .Define(new Map<RelationshipByFrom>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.FromEntityId)
                .ClusteringKey(t => t.ToEntityId));
        var byToMapping = new MappingConfiguration()
            .Define(new Map<RelationshipByTo>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.ToEntityId)
                .ClusteringKey(t => t.FromEntityId));
        _relationshipsByFrom = new Table<RelationshipByFrom>(session, byFromMapping);
        _relationshipsByTo = new Table<RelationshipByTo>(session, byToMapping);
        _relationshipTypes = new Table<RelationshipType>(session, typeMapping);

        // Initialize default relationship types asynchronously (does not touch schema)
        _ = InitializeDefaultRelationshipTypes();
    }

    /// <summary>
    /// Ensure relationship-related schema exists and is configured. Called by the centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        _relationships.CreateIfNotExists();
        _relationshipsByFrom.CreateIfNotExists();
        _relationshipsByTo.CreateIfNotExists();
        _relationshipTypes.CreateIfNotExists();

        TryEnsureLcs("relationship");
        TryEnsureLcs("relationship_by_from");
        TryEnsureLcs("relationship_by_to");
        TryEnsureLcs("relationship_type");
    }

    private void TryEnsureLcs(string tableName)
    {
        // Wait briefly for table metadata to appear in system_schema (schema propagation) before attempting ALTER.
        try
        {
            var keyspace = _session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                _logger.LogDebug("Session has no keyspace; skipping LCS check for {Table}", tableName);
                return;
            }

            if (_session == null)
            {
                _logger.LogDebug("No session available when checking compaction for {Table}", tableName);
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
                return; // table not visible yet
            }

            try
            {
                var compaction = row.GetValue<IDictionary<string, string>>("compaction");
                if (compaction != null && compaction.TryGetValue("class", out var cls) &&
                    !string.IsNullOrEmpty(cls) && cls.Contains("LeveledCompactionStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Table {Table} already uses compaction {Class}", tableName, cls);
                    return; // already set
                }
            }
            catch
            {
                // ignore parse errors and fall through to attempt ALTER
            }

            var target = $"{keyspace}.{tableName}";
            var cql = $"ALTER TABLE {target} WITH compaction = {{'class':'LeveledCompactionStrategy'}}";
            _session.Execute(new global::Cassandra.SimpleStatement(cql));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to ensure LCS for table {Table}", tableName);
        }
    }

    /// <summary>
    /// Create a bidirectional relationship
    /// </summary>
    public async Task<(Relationship primary, Relationship inverse)> CreateRelationship(
        Guid userId,
        EntityType fromType, Guid fromId,
        EntityType toType, Guid toId,
        string relationshipType,
        string language = "de",
        DateTime? startDate = null,
        DateTime? endDate = null,
        int certainty = 100,
        string? source = null,
        string? notes = null)
    {
        var relationshipId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Get inverse relationship type if it exists
        var inverseType = await GetInverseRelationshipType(relationshipType, language);

        // Create primary relationship
        var primary = new Relationship
        {
            Id = relationshipId,
            UserId = userId,
            FromEntityType = fromType,
            FromEntityId = fromId,
            ToEntityType = toType,
            ToEntityId = toId,
            RelationshipType = relationshipType,
            Language = language,
            StartDate = startDate,
            EndDate = endDate,
            Certainty = certainty,
            Source = source,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            IsPrimary = true
        };

        // Create inverse relationship
        var inverse = new Relationship
        {
            Id = relationshipId, // Same ID for linking
            UserId = userId,
            FromEntityType = toType, // Swapped
            FromEntityId = toId, // Swapped
            ToEntityType = fromType, // Swapped
            ToEntityId = fromId, // Swapped
            RelationshipType = inverseType ?? relationshipType, // Use inverse or same
            Language = language,
            StartDate = startDate,
            EndDate = endDate,
            Certainty = certainty,
            Source = source,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            IsPrimary = false
        };

        await _relationships.Insert(primary).ExecuteAsync();
        await _relationships.Insert(inverse).ExecuteAsync();
        // maintain denormalized tables for reads
        var pf = new RelationshipByFrom
        {
            UserId = primary.UserId,
            FromEntityId = primary.FromEntityId,
            ToEntityId = primary.ToEntityId,
            Id = primary.Id,
            FromEntityType = primary.FromEntityType,
            ToEntityType = primary.ToEntityType,
            RelationshipType = primary.RelationshipType,
            Language = primary.Language,
            StartDate = primary.StartDate,
            EndDate = primary.EndDate,
            Certainty = primary.Certainty,
            Source = primary.Source,
            Notes = primary.Notes,
            CreatedAt = primary.CreatedAt,
            UpdatedAt = primary.UpdatedAt,
            IsPrimary = primary.IsPrimary
        };

        var pi = new RelationshipByFrom
        {
            UserId = inverse.UserId,
            FromEntityId = inverse.FromEntityId,
            ToEntityId = inverse.ToEntityId,
            Id = inverse.Id,
            FromEntityType = inverse.FromEntityType,
            ToEntityType = inverse.ToEntityType,
            RelationshipType = inverse.RelationshipType,
            Language = inverse.Language,
            StartDate = inverse.StartDate,
            EndDate = inverse.EndDate,
            Certainty = inverse.Certainty,
            Source = inverse.Source,
            Notes = inverse.Notes,
            CreatedAt = inverse.CreatedAt,
            UpdatedAt = inverse.UpdatedAt,
            IsPrimary = inverse.IsPrimary
        };

        await _relationshipsByFrom.Insert(pf).ExecuteAsync();
        await _relationshipsByFrom.Insert(pi).ExecuteAsync();

        // also insert into by-to table
        var tf = new RelationshipByTo
        {
            UserId = primary.UserId,
            ToEntityId = primary.ToEntityId,
            FromEntityId = primary.FromEntityId,
            Id = primary.Id,
            FromEntityType = primary.FromEntityType,
            ToEntityType = primary.ToEntityType,
            RelationshipType = primary.RelationshipType,
            Language = primary.Language,
            StartDate = primary.StartDate,
            EndDate = primary.EndDate,
            Certainty = primary.Certainty,
            Source = primary.Source,
            Notes = primary.Notes,
            CreatedAt = primary.CreatedAt,
            UpdatedAt = primary.UpdatedAt,
            IsPrimary = primary.IsPrimary
        };

        var ti = new RelationshipByTo
        {
            UserId = inverse.UserId,
            ToEntityId = inverse.ToEntityId,
            FromEntityId = inverse.FromEntityId,
            Id = inverse.Id,
            FromEntityType = inverse.FromEntityType,
            ToEntityType = inverse.ToEntityType,
            RelationshipType = inverse.RelationshipType,
            Language = inverse.Language,
            StartDate = inverse.StartDate,
            EndDate = inverse.EndDate,
            Certainty = inverse.Certainty,
            Source = inverse.Source,
            Notes = inverse.Notes,
            CreatedAt = inverse.CreatedAt,
            UpdatedAt = inverse.UpdatedAt,
            IsPrimary = inverse.IsPrimary
        };

        await _relationshipsByTo.Insert(tf).ExecuteAsync();
        await _relationshipsByTo.Insert(ti).ExecuteAsync();

        _logger.LogInformation(
            "Created bidirectional relationship {RelationshipId}: {FromType}:{FromId} -{Type}-> {ToType}:{ToId}",
            relationshipId, fromType, fromId, relationshipType, toType, toId);

        return (primary, inverse);
    }

    /// <summary>
    /// Get all relationships for an entity
    /// </summary>
    public async Task<IEnumerable<Relationship>> GetRelationshipsForEntity(Guid userId, Guid entityId, bool primaryOnly = false)
    {
        // Read-optimized: query denormalized table by FromEntityId
        var rows = await _relationshipsByFrom
            .Where(r => r.UserId == userId && r.FromEntityId == entityId)
            .ExecuteAsync();

        var results = rows.Select(r => new Relationship
        {
            UserId = r.UserId,
            Id = r.Id,
            FromEntityType = r.FromEntityType,
            FromEntityId = r.FromEntityId,
            ToEntityType = r.ToEntityType,
            ToEntityId = r.ToEntityId,
            RelationshipType = r.RelationshipType,
            Language = r.Language,
            StartDate = r.StartDate,
            EndDate = r.EndDate,
            Certainty = r.Certainty,
            Source = r.Source,
            Notes = r.Notes,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            IsPrimary = r.IsPrimary
        });

        if (primaryOnly)
        {
            results = results.Where(r => r.IsPrimary);
        }

        return results;
    }

    /// <summary>
    /// Get specific relationship between two entities
    /// </summary>
    public async Task<Relationship?> GetRelationship(Guid userId, Guid fromId, Guid toId, string? relationshipType = null)
    {
        var all = await _relationships
            .Where(r => r.UserId == userId)
            .Take(10000)
            .ExecuteAsync();

    var query = all.Where(r => r.FromEntityId == fromId && r.ToEntityId == toId);

        if (!string.IsNullOrEmpty(relationshipType))
        {
            query = query.Where(r => r.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase));
        }

        return query.FirstOrDefault();
    }

    /// <summary>
    /// Get all relationships of a specific type for an entity
    /// </summary>
    public async Task<IEnumerable<Relationship>> GetRelationshipsByType(Guid userId, Guid entityId, string relationshipType)
    {
        var all = await GetRelationshipsForEntity(userId, entityId);
        return all.Where(r => r.RelationshipType.Equals(relationshipType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Find relationship path between two entities (e.g., "John's Uncle")
    /// </summary>
    public async Task<List<Relationship>> FindRelationshipPath(Guid userId, Guid startEntityId, Guid endEntityId, int maxDepth = 3)
    {
    var visited = new HashSet<Guid>();
        var path = new List<Relationship>();
        
        if (await FindPathRecursive(userId, startEntityId, endEntityId, visited, path, maxDepth))
        {
            return path;
        }

        return new List<Relationship>();
    }

    private async Task<bool> FindPathRecursive(Guid userId, Guid currentId, Guid targetId, HashSet<Guid> visited, List<Relationship> path, int remainingDepth)
    {
        if (currentId == targetId)
            return true;

        if (remainingDepth <= 0 || visited.Contains(currentId))
            return false;

    visited.Add(currentId);

    var relationships = await GetRelationshipsForEntity(userId, currentId);

        foreach (var relationship in relationships)
        {
            if (!visited.Contains(relationship.ToEntityId))
            {
                path.Add(relationship);
                
                if (await FindPathRecursive(userId, relationship.ToEntityId, targetId, visited, path, remainingDepth - 1))
                {
                    return true;
                }
                
                path.RemoveAt(path.Count - 1);
            }
        }

        return false;
    }

    /// <summary>
    /// Update a relationship
    /// </summary>
    public async Task UpdateRelationship(Relationship relationship)
    {
        relationship.UpdatedAt = DateTime.UtcNow;
        await _relationships.Insert(relationship).ExecuteAsync();
        _logger.LogInformation("Updated relationship {RelationshipId}", relationship.Id);
    }

    /// <summary>
    /// Delete a bidirectional relationship
    /// </summary>
    public async Task DeleteRelationship(Guid userId, Guid relationshipId)
    {
        // Need to delete both primary and inverse - query rows first then delete with parameters
        var all = await _relationships
            .Where(r => r.UserId == userId && r.Id == relationshipId)
            .ExecuteAsync();

        foreach (var rel in all)
        {
            var ps = _session.Prepare("DELETE FROM relationship WHERE user_id = ? AND id = ? AND from_entity_id = ? AND to_entity_id = ?");
            var bound = ps.Bind(rel.UserId, rel.Id, rel.FromEntityId, rel.ToEntityId);
            await _session.ExecuteAsync(bound);
        }

        _logger.LogInformation("Deleted relationship {RelationshipId} for user {UserId}", relationshipId, userId);
    }

    /// <summary>
    /// Add a relationship type with translation
    /// </summary>
    public async Task AddRelationshipType(string type, string language, string displayName, string? inverseType = null, string category = "general")
    {
        var relType = new RelationshipType
        {
            Type = type,
            Language = language,
            DisplayName = displayName,
            InverseType = inverseType,
            Category = category
        };

        await _relationshipTypes.Insert(relType).ExecuteAsync();
        _logger.LogInformation("Added relationship type {Type} ({Language}): {DisplayName}", type, language, displayName);
    }

    /// <summary>
    /// Get relationship type translations
    /// </summary>
    public async Task<IEnumerable<RelationshipType>> GetRelationshipTypes(string? language = null)
    {
        if (!string.IsNullOrEmpty(language))
        {
            var all = await _relationshipTypes.ExecuteAsync();
            return all.Where(rt => rt.Language == language);
        }

        return await _relationshipTypes.ExecuteAsync();
    }

    /// <summary>
    /// Get inverse relationship type
    /// </summary>
    private async Task<string?> GetInverseRelationshipType(string type, string language)
    {
        var relType = await _relationshipTypes
            .Where(rt => rt.Type == type && rt.Language == language)
            .FirstOrDefault()
            .ExecuteAsync();

        return relType?.InverseType;
    }

    /// <summary>
    /// Initialize default relationship types
    /// </summary>
    private async Task InitializeDefaultRelationshipTypes()
    {
        try
        {
            // German family relationships
            await AddRelationshipType("Mutter", "de", "Mutter", "Kind", "family");
            await AddRelationshipType("Vater", "de", "Vater", "Kind", "family");
            await AddRelationshipType("Kind", "de", "Kind", "Elternteil", "family");
            await AddRelationshipType("Elternteil", "de", "Elternteil", "Kind", "family");
            await AddRelationshipType("Ehepartner", "de", "Ehepartner", "Ehepartner", "family");
            await AddRelationshipType("Bruder", "de", "Bruder", "Geschwister", "family");
            await AddRelationshipType("Schwester", "de", "Schwester", "Geschwister", "family");
            await AddRelationshipType("Geschwister", "de", "Geschwister", "Geschwister", "family");
            await AddRelationshipType("Großmutter", "de", "Großmutter", "Enkelkind", "family");
            await AddRelationshipType("Großvater", "de", "Großvater", "Enkelkind", "family");
            await AddRelationshipType("Enkelkind", "de", "Enkelkind", "Großelternteil", "family");
            await AddRelationshipType("Onkel", "de", "Onkel", "Neffe/Nichte", "family");
            await AddRelationshipType("Tante", "de", "Tante", "Neffe/Nichte", "family");

            // English family relationships
            await AddRelationshipType("Mother", "en", "Mother", "Child", "family");
            await AddRelationshipType("Father", "en", "Father", "Child", "family");
            await AddRelationshipType("Child", "en", "Child", "Parent", "family");
            await AddRelationshipType("Parent", "en", "Parent", "Child", "family");
            await AddRelationshipType("Spouse", "en", "Spouse", "Spouse", "family");
            await AddRelationshipType("Brother", "en", "Brother", "Sibling", "family");
            await AddRelationshipType("Sister", "en", "Sister", "Sibling", "family");
            await AddRelationshipType("Sibling", "en", "Sibling", "Sibling", "family");

            // Ownership relationships (German)
            await AddRelationshipType("Besitzer", "de", "Besitzer", "Besitz", "ownership");
            await AddRelationshipType("Besitz", "de", "Besitz", "Besitzer", "ownership");

            // Ownership relationships (English)
            await AddRelationshipType("Owner", "en", "Owner", "Owned", "ownership");
            await AddRelationshipType("Owned", "en", "Owned by", "Owner", "ownership");

            _logger.LogInformation("Initialized default relationship types");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize some relationship types (may already exist)");
        }
    }
}
