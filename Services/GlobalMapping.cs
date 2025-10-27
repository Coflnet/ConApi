using Cassandra.Mapping;
using Coflnet.Connections.Models;
using Coflnet.Connections.DTOs;

namespace Coflnet.Connections.Services;

/// <summary>
/// Centralized mapping configuration used by all services.
/// Defining mappings in one place reduces duplication and ensures CreateIfNotExists
/// uses the same mapping information across the app.
/// </summary>
public static class GlobalMapping
{
    private static readonly Lazy<MappingConfiguration> _instance = new(() => CreateConfiguration());

    public static MappingConfiguration Instance => _instance.Value;

    private static MappingConfiguration CreateConfiguration()
    {
        var cfg = new MappingConfiguration();

        // SearchEntry
        cfg.Define(new Map<SearchEntry>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.KeyWord)
            .ClusteringKey(t => t.Type)
            .ClusteringKey(t => t.FullId)
            .Column(o => o.Type, c => c.WithName("type").WithDbType<int>()));

        // Place
        cfg.Define(new Map<Place>()
            .PartitionKey(t => t.UserId, t => t.Name)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        cfg.Define(new Map<PlaceData>()
            .PartitionKey(t => t.UserId, t => t.PlaceId)
            .ClusteringKey(t => t.Category)
            .ClusteringKey(t => t.Key));

        // Thing
        cfg.Define(new Map<Thing>()
            .PartitionKey(t => t.UserId, t => t.Name)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        cfg.Define(new Map<ThingData>()
            .PartitionKey(t => t.UserId, t => t.ThingId)
            .ClusteringKey(t => t.Category)
            .ClusteringKey(t => t.Key));

        cfg.Define(new Map<ThingByOwner>()
            .TableName("thing_by_owner")
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.OwnerId)
            .ClusteringKey(t => t.ThingId)
            .Column(t => t.UserId, c => c.WithName("user_id"))
            .Column(t => t.OwnerId, c => c.WithName("owner_id"))
            .Column(t => t.ThingId, c => c.WithName("thing_id"))
            .Column(t => t.Name, c => c.WithName("name"))
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
            .Column(t => t.CreatedAt, c => c.WithName("created_at"))
            .Column(t => t.UpdatedAt, c => c.WithName("updated_at")));

        // Event
        cfg.Define(new Map<Event>()
            // Partition by user and year so timeline queries can target one year partition at a time
            .PartitionKey(t => t.UserId, t => t.EventYear)
            .TableName("event")
            // First clustering key: the target entity (person/place/thing) so we can query by it
            .ClusteringKey(t => t.TargetEntityId)
            .ClusteringKey(t => t.EventDate)
            .ClusteringKey(t => t.Id)
            .Column(t => t.EventYear, c => c.WithName("event_year"))
            .Column(t => t.UserId, c => c.WithName("user_id"))
            .Column(t => t.Title, c => c.WithName("title"))
            .Column(t => t.EventDate, c => c.WithName("event_date"))
            .Column(t => t.Id, c => c.WithName("id"))
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
            .Column(t => t.TargetEntityType, c => c.WithName("target_entity_type").WithDbType<int>())
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>())
            .Column(t => t.Attributes, c => c.WithName("attributes"))
            .Column(t => t.TargetEntityId, c => c.WithName("target_entity_id"))
            .Column(t => t.PlaceId, c => c.WithName("place_id"))
            .Column(t => t.Description, c => c.WithName("description"))
            .Column(t => t.CreatedAt, c => c.WithName("created_at"))
            .Column(t => t.UpdatedAt, c => c.WithName("updated_at"))
            .Column(t => t.EndDate, c => c.WithName("end_date")));

        
            
        // Map EventData changed_at
        cfg.Define(new Map<EventData>()
            .PartitionKey(t => t.UserId, t => t.EventId)
            .ClusteringKey(t => t.Category)
            .ClusteringKey(t => t.Key)
            .Column(t => t.Value, c => c.WithName("value"))
            .Column(t => t.ChangedAt, c => c.WithName("changed_at")));

        // Relationship (explicit table name to match CQL created table)
        cfg.Define(new Map<Relationship>()
            .TableName("relationship")
            .PartitionKey(t => t.UserId, t => t.Id)
            .Column(t => t.UserId, c => c.WithName("user_id"))
            .ClusteringKey(t => t.FromEntityId)
            .ClusteringKey(t => t.ToEntityId)
            .Column(t => t.StartDate, c => c.WithName("start_date"))
            .Column(t => t.EndDate, c => c.WithName("end_date"))
            .Column(t => t.Id, c => c.WithName("id"))
            .Column(t => t.FromEntityId, c => c.WithName("from_entity_id"))
            .Column(t => t.ToEntityId, c => c.WithName("to_entity_id"))
            .Column(t => t.RelationshipType, c => c.WithName("relationship_type"))
            .Column(t => t.Language, c => c.WithName("language"))
            .Column(t => t.FromEntityType, c => c.WithName("from_entity_type").WithDbType<int>())
            .Column(t => t.ToEntityType, c => c.WithName("to_entity_type").WithDbType<int>())
            .Column(t => t.CreatedAt, c => c.WithName("created_at"))
            .Column(t => t.UpdatedAt, c => c.WithName("updated_at"))
            .Column(t => t.IsPrimary, c => c.WithName("is_primary")));

        cfg.Define(new Map<RelationshipByFrom>()
            .TableName("relationship_by_from")
            .PartitionKey(t => t.UserId)
            .Column(t => t.UserId, c => c.WithName("user_id"))
            .ClusteringKey(t => t.FromEntityId)
            .ClusteringKey(t => t.ToEntityId)
            .Column(t => t.StartDate, c => c.WithName("start_date"))
            .Column(t => t.EndDate, c => c.WithName("end_date"))
            .Column(t => t.Id, c => c.WithName("id"))
            .Column(t => t.FromEntityId, c => c.WithName("from_entity_id"))
            .Column(t => t.ToEntityId, c => c.WithName("to_entity_id"))
            .Column(t => t.RelationshipType, c => c.WithName("relationship_type"))
            .Column(t => t.Language, c => c.WithName("language"))
            .Column(t => t.FromEntityType, c => c.WithName("from_entity_type").WithDbType<int>())
            .Column(t => t.ToEntityType, c => c.WithName("to_entity_type").WithDbType<int>())
            .Column(t => t.CreatedAt, c => c.WithName("created_at"))
            .Column(t => t.UpdatedAt, c => c.WithName("updated_at"))
            .Column(t => t.IsPrimary, c => c.WithName("is_primary")));

        cfg.Define(new Map<RelationshipByTo>()
            .TableName("relationship_by_to")
            .PartitionKey(t => t.UserId)
            .Column(t => t.UserId, c => c.WithName("user_id"))
            .ClusteringKey(t => t.ToEntityId)
            .ClusteringKey(t => t.FromEntityId)
            .Column(t => t.StartDate, c => c.WithName("start_date"))
            .Column(t => t.EndDate, c => c.WithName("end_date"))
            .Column(t => t.Id, c => c.WithName("id"))
            .Column(t => t.FromEntityId, c => c.WithName("from_entity_id"))
            .Column(t => t.ToEntityId, c => c.WithName("to_entity_id"))
            .Column(t => t.RelationshipType, c => c.WithName("relationship_type"))
            .Column(t => t.Language, c => c.WithName("language"))
            .Column(t => t.FromEntityType, c => c.WithName("from_entity_type").WithDbType<int>())
            .Column(t => t.ToEntityType, c => c.WithName("to_entity_type").WithDbType<int>())
            .Column(t => t.CreatedAt, c => c.WithName("created_at"))
            .Column(t => t.UpdatedAt, c => c.WithName("updated_at"))
            .Column(t => t.IsPrimary, c => c.WithName("is_primary")));

        // Map relationship_type table columns to snake_case
        cfg.Define(new Map<RelationshipType>()
            .TableName("relationship_type")
            .PartitionKey(t => t.Type)
            .ClusteringKey(t => t.Language)
            .Column(t => t.DisplayName, c => c.WithName("display_name"))
            .Column(t => t.InverseType, c => c.WithName("inverse_type"))
            .Column(t => t.Category, c => c.WithName("category")));

        // Person
        cfg.Define(new Map<Person>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Gender, c => c.WithName("gender").WithDbType<int>())
            .TableName("person2")
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        // Share
        cfg.Define(new Map<ShareInvitation>()
            .PartitionKey(t => t.UserId, t => t.FromUserId)
            .ClusteringKey(t => t.ToUserId)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Status, c => c.WithName("status").WithDbType<int>())
            .Column(t => t.Permission, c => c.WithName("permission").WithDbType<int>())
            .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>())
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        cfg.Define(new Map<ShareInvitationByRecipient>()
            .PartitionKey(t => t.ToUserId)
            .ClusteringKey(t => t.Status)
            .ClusteringKey(t => t.InvitationId)
            .Column(t => t.Status, c => c.WithName("status").WithDbType<int>())
            .Column(t => t.Permission, c => c.WithName("permission").WithDbType<int>())
            .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>()));

        cfg.Define(new Map<DataProvenance>()
            .PartitionKey(t => t.EntityId)
            .ClusteringKey(t => t.ChangedAt)
            .Column(t => t.ChangeType, c => c.WithName("change_type").WithDbType<int>()));

        cfg.Define(new Map<DataConflict>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.EntityId)
            .ClusteringKey(t => t.FieldName)
            .Column(t => t.Resolution, c => c.WithName("resolution").WithDbType<int>()));

        // Document
        cfg.Define(new Map<Document>()
            .PartitionKey(t => t.UserId, t => t.FileName)
            .ClusteringKey(t => t.Id)
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
            .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));

        cfg.Define(new Map<DocumentLink>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.EntityId)
            .ClusteringKey(t => t.DocumentId)
            .Column(t => t.EntityType, c => c.WithName("entity_type").WithDbType<int>()));

        cfg.Define(new Map<DocumentByEntity>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.EntityId)
            .ClusteringKey(t => t.DisplayOrder)
            .ClusteringKey(t => t.DocumentId)
            .Column(t => t.Type, c => c.WithName("type").WithDbType<int>()));

        cfg.Define(new Map<StorageQuota>()
            .PartitionKey(t => t.UserId));

        // SourceCitation
        cfg.Define(new Map<SourceCitation>()
            .PartitionKey(c => c.UserId)
            .ClusteringKey(c => c.EntityId)
            .ClusteringKey(c => c.FieldName)
            .Column(c => c.EntityType, col => col.WithName("entity_type").WithDbType<int>())
            .Column(c => c.SourceType, col => col.WithName("source_type").WithDbType<int>())
            .Column(c => c.PrivacyLevel, col => col.WithName("privacy_level").WithDbType<int>()));

        cfg.Define(new Map<CitationBySource>()
            .PartitionKey(c => c.UserId)
            .ClusteringKey(c => c.Title)
            .ClusteringKey(c => c.CitationId)
            .Column(c => c.EntityType, col => col.WithName("entity_type").WithDbType<int>()));

        cfg.Define(new Map<ConflictingInformation>()
            .PartitionKey(c => c.UserId)
            .ClusteringKey(c => c.EntityId)
            .ClusteringKey(c => c.FieldName)
            .Column(c => c.Resolution, col => col.WithName("resolution").WithDbType<int>())
            .Column(c => c.PrivacyLevel, col => col.WithName("privacy_level").WithDbType<int>()));

        return cfg;
    }
}
