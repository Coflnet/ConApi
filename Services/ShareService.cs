using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Connections.DTOs;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing data sharing between users
/// </summary>
public class ShareService
{
    private readonly ISession _session;
    private readonly Table<ShareInvitation> _invitations;
    private readonly Table<ShareInvitationByRecipient> _invitationsByRecipient;
    private readonly Table<DataProvenance> _provenance;
    private readonly Table<DataConflict> _conflicts;
    private readonly ILogger<ShareService> _logger;

    public ShareService(ISession session, ILogger<ShareService> logger)
    {
        _session = session;
        _logger = logger;

        var invitationMapping = new MappingConfiguration()
            .Define(new Map<ShareInvitation>()
                .PartitionKey(t => t.UserId, t => t.FromUserId)
                .ClusteringKey(t => t.ToUserId)
                .ClusteringKey(t => t.Id)
                .Column(t => t.Status, c => c.WithName("status").WithDbType<int>())
                .Column(t => t.Permission, c => c.WithName("permission").WithDbType<int>())
                .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>())
                .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        var byRecipientMapping = new MappingConfiguration()
            .Define(new Map<ShareInvitationByRecipient>()
                .PartitionKey(t => t.ToUserId)
                .ClusteringKey(t => t.Status)
                .ClusteringKey(t => t.InvitationId)
                .Column(t => t.Status, c => c.WithName("status").WithDbType<int>())
                .Column(t => t.Permission, c => c.WithName("permission").WithDbType<int>())
                .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>()));

        var provenanceMapping = new MappingConfiguration()
            .Define(new Map<DataProvenance>()
                .PartitionKey(t => t.EntityId)
                .ClusteringKey(t => t.ChangedAt)
                .Column(t => t.ChangeType, c => c.WithName("change_type").WithDbType<int>()));

        var conflictMapping = new MappingConfiguration()
            .Define(new Map<DataConflict>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.EntityId)
                .ClusteringKey(t => t.FieldName)
                .Column(t => t.Resolution, c => c.WithName("resolution").WithDbType<int>()));

        _invitations = new Table<ShareInvitation>(session, invitationMapping);
        _invitationsByRecipient = new Table<ShareInvitationByRecipient>(session, byRecipientMapping);
        _provenance = new Table<DataProvenance>(session, provenanceMapping);
        _conflicts = new Table<DataConflict>(session, conflictMapping);
    }

    /// <summary>
    /// Create a sharing invitation
    /// </summary>
    public async Task<ShareInvitation> CreateInvitation(Guid fromUserId, CreateShareInvitationDto dto)
    {
        var invitation = new ShareInvitation
        {
            Id = Guid.NewGuid(),
            UserId = fromUserId,
            FromUserId = fromUserId,
            ToUserId = dto.ToUserId,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Permission = dto.Permission,
            Message = dto.Message,
            Status = ShareStatus.Pending,
            ExpiresAt = dto.ExpiresAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _invitations.Insert(invitation).ExecuteAsync();

        // Denormalized table for recipient queries
        var byRecipient = new ShareInvitationByRecipient
        {
            ToUserId = dto.ToUserId,
            Status = ShareStatus.Pending,
            InvitationId = invitation.Id,
            FromUserId = fromUserId,
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            Permission = dto.Permission,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt
        };

        await _invitationsByRecipient.Insert(byRecipient).ExecuteAsync();

        _logger.LogInformation("Created share invitation {InvitationId} from {FromUser} to {ToUser}",
            invitation.Id, fromUserId, dto.ToUserId);

        return invitation;
    }

    /// <summary>
    /// Get invitations for a user (sent or received)
    /// </summary>
    public async Task<IEnumerable<ShareInvitation>> GetInvitationsByUser(Guid userId, bool sent = false)
    {
        if (sent)
        {
            return await _invitations
                .Where(i => i.UserId == userId && i.FromUserId == userId)
                .ExecuteAsync();
        }
        else
        {
            return await _invitations
                .Where(i => i.UserId == userId)
                .ExecuteAsync();
        }
    }

    /// <summary>
    /// Get pending invitations for a recipient
    /// </summary>
    public async Task<IEnumerable<ShareInvitationByRecipient>> GetPendingInvitations(Guid toUserId)
    {
        return await _invitationsByRecipient
            .Where(i => i.ToUserId == toUserId && i.Status == ShareStatus.Pending)
            .ExecuteAsync();
    }

    /// <summary>
    /// Accept or reject an invitation
    /// </summary>
    public async Task<ShareInvitation?> RespondToInvitation(Guid userId, Guid invitationId, bool accept, ConflictResolution? defaultResolution = null)
    {
        var invitation = await GetInvitationById(invitationId);
        if (invitation == null || invitation.ToUserId != userId)
        {
            _logger.LogWarning("Invitation {InvitationId} not found or not for user {UserId}", invitationId, userId);
            return null;
        }

        if (invitation.Status != ShareStatus.Pending)
        {
            _logger.LogWarning("Invitation {InvitationId} is not pending (status: {Status})", invitationId, invitation.Status);
            return null;
        }

        invitation.Status = accept ? ShareStatus.Accepted : ShareStatus.Rejected;
        invitation.AcceptedAt = accept ? DateTime.UtcNow : null;
        invitation.UpdatedAt = DateTime.UtcNow;

        await _invitations.Insert(invitation).ExecuteAsync();

        _logger.LogInformation("User {UserId} {Action} invitation {InvitationId}",
            userId, accept ? "accepted" : "rejected", invitationId);

        return invitation;
    }

    /// <summary>
    /// Get invitation by ID
    /// </summary>
    public async Task<ShareInvitation?> GetInvitationById(Guid invitationId)
    {
        var result = await _invitations
            .Where(i => i.Id == invitationId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Track data provenance
    /// </summary>
    public async Task TrackChange(Guid entityId, Guid changedBy, ChangeType changeType, 
        string? fieldName = null, string? oldValue = null, string? newValue = null, string? source = null)
    {
        var provenance = new DataProvenance
        {
            EntityId = entityId,
            ChangedAt = DateTime.UtcNow,
            ChangedBy = changedBy,
            ChangeType = changeType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = source
        };

        await _provenance.Insert(provenance).ExecuteAsync();

        _logger.LogDebug("Tracked change for entity {EntityId} by user {UserId}: {ChangeType}",
            entityId, changedBy, changeType);
    }

    /// <summary>
    /// Get change history for an entity
    /// </summary>
    public async Task<IEnumerable<DataProvenance>> GetChangeHistory(Guid entityId, int limit = 100)
    {
        return await _provenance
            .Where(p => p.EntityId == entityId)
            .Take(limit)
            .ExecuteAsync();
    }

    /// <summary>
    /// Create a data conflict record
    /// </summary>
    public async Task<DataConflict> CreateConflict(Guid userId, Guid entityId, string fieldName,
        string? localValue, string? remoteValue, Guid remoteUserId)
    {
        var conflict = new DataConflict
        {
            UserId = userId,
            EntityId = entityId,
            FieldName = fieldName,
            LocalValue = localValue,
            RemoteValue = remoteValue,
            RemoteUserId = remoteUserId,
            DetectedAt = DateTime.UtcNow
        };

        await _conflicts.Insert(conflict).ExecuteAsync();

        _logger.LogInformation("Created conflict for entity {EntityId}, field {Field}", entityId, fieldName);

        return conflict;
    }

    /// <summary>
    /// Get unresolved conflicts for a user
    /// </summary>
    public async Task<IEnumerable<DataConflict>> GetUnresolvedConflicts(Guid userId)
    {
        var all = await _conflicts
            .Where(c => c.UserId == userId)
            .ExecuteAsync();

        return all.Where(c => c.Resolution == null);
    }

    /// <summary>
    /// Resolve a conflict
    /// </summary>
    public async Task<DataConflict?> ResolveConflict(Guid userId, Guid entityId, string fieldName, ConflictResolution resolution)
    {
        var conflicts = await _conflicts
            .Where(c => c.UserId == userId && c.EntityId == entityId && c.FieldName == fieldName)
            .ExecuteAsync();

        var conflict = conflicts.FirstOrDefault();
        if (conflict == null)
        {
            return null;
        }

        conflict.Resolution = resolution;
        conflict.ResolvedAt = DateTime.UtcNow;

        await _conflicts.Insert(conflict).ExecuteAsync();

        _logger.LogInformation("Resolved conflict for entity {EntityId}, field {Field} with {Resolution}",
            entityId, fieldName, resolution);

        return conflict;
    }

    /// <summary>
    /// Ensure share-related schema exists
    /// </summary>
    public void EnsureSchema()
    {
        _invitations.CreateIfNotExists();
        _invitationsByRecipient.CreateIfNotExists();
        _provenance.CreateIfNotExists();
        _conflicts.CreateIfNotExists();

        TryEnsureLcs("share_invitation");
        TryEnsureLcs("share_invitation_by_recipient");
        TryEnsureLcs("data_provenance");
        TryEnsureLcs("data_conflict");
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
