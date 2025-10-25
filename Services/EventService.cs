using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Connections.Models;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing events and timelines
/// </summary>
public class EventService
{
    private readonly ISession _session;
    private readonly Table<Event> _events;
    private readonly Table<EventData> _eventData;
    private readonly ILogger<EventService> _logger;
    private readonly SearchService _searchService;

    public EventService(ISession session, ILogger<EventService> logger, SearchService searchService)
    {
        _session = session;
        _logger = logger;
        _searchService = searchService;
        
        var eventMapping = new MappingConfiguration()
            .Define(new Map<Event>()
                .PartitionKey(t => t.UserId, t => t.Title)
                .ClusteringKey(t => t.EventDate)
                .ClusteringKey(t => t.TargetEntityId)
                .Column(t => t.Type, c => c.WithName("type").WithDbType<int>())
                .Column(t => t.TargetEntityType, c => c.WithName("target_entity_type").WithDbType<int>())
                .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>()));
                
        var eventDataMapping = new MappingConfiguration()
            .Define(new Map<EventData>()
                .PartitionKey(t => t.UserId, t => t.EventId)
                .ClusteringKey(t => t.Category)
                .ClusteringKey(t => t.Key));
        
        _events = new Table<Event>(session, eventMapping);
        _eventData = new Table<EventData>(session, eventDataMapping);
    }

    /// <summary>
    /// Get an event by ID
    /// </summary>
    public async Task<Event?> GetEventById(Guid userId, Guid eventId)
    {
        var result = await _events
            .Where(e => e.UserId == userId && e.Id == eventId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Get timeline for an entity (person, place, or thing)
    /// </summary>
    public async Task<IEnumerable<Event>> GetTimeline(Guid userId, Guid targetEntityId, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Note: Requires materialized view or secondary index for production
        _logger.LogWarning("GetTimeline performs client-side filtering - consider adding materialized view");
        
        var query = _events.Where(e => e.UserId == userId).Take(10000);
        var allEvents = await query.ExecuteAsync();
        
    var filtered = allEvents.Where(e => e.TargetEntityId == targetEntityId);
        
        if (startDate.HasValue)
        {
            filtered = filtered.Where(e => e.EventDate >= startDate.Value);
        }
        
        if (endDate.HasValue)
        {
            filtered = filtered.Where(e => e.EventDate <= endDate.Value);
        }
        
        return filtered.OrderBy(e => e.EventDate);
    }

    /// <summary>
    /// Get events by type
    /// </summary>
    public async Task<IEnumerable<Event>> GetEventsByType(Guid userId, EventType type, int limit = 100)
    {
        _logger.LogWarning("GetEventsByType performs client-side filtering - consider adding materialized view");
        
        var allEvents = await _events
            .Where(e => e.UserId == userId)
            .Take(10000)
            .ExecuteAsync();
        
        return allEvents
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.EventDate)
            .Take(limit);
    }

    /// <summary>
    /// Get events in a date range
    /// </summary>
    public async Task<IEnumerable<Event>> GetEventsByDateRange(Guid userId, DateTime startDate, DateTime endDate)
    {
        _logger.LogWarning("GetEventsByDateRange performs client-side filtering - consider adding materialized view");
        
        var allEvents = await _events
            .Where(e => e.UserId == userId)
            .Take(10000)
            .ExecuteAsync();
        
        return allEvents
            .Where(e => e.EventDate >= startDate && e.EventDate <= endDate)
            .OrderBy(e => e.EventDate);
    }

    /// <summary>
    /// Add or update an event
    /// </summary>
    public async Task<Event> SaveEvent(Event ev)
    {
        if (ev.Id == Guid.Empty)
        {
            ev.Id = Guid.NewGuid();
        }
        
        ev.UpdatedAt = DateTime.UtcNow;
        if (ev.CreatedAt == default)
        {
            ev.CreatedAt = DateTime.UtcNow;
        }
        
        await _events.Insert(ev).ExecuteAsync();

        try
        {
            if (!string.IsNullOrEmpty(ev.Title))
            {
                await _searchService.AddEntry(ev.UserId, ev.Title, ev.Id.ToString(), SearchEntry.ResultType.Unknown);
            }
        }
        catch { }
        _logger.LogInformation("Saved event {EventId} '{EventTitle}' on {EventDate} for user {UserId}", 
            ev.Id, ev.Title, ev.EventDate, ev.UserId);
        
        return ev;
    }

    /// <summary>
    /// Add flexible attribute to an event
    /// </summary>
    public async Task AddEventData(EventData data)
    {
        data.ChangedAt = DateTime.UtcNow;
        await _eventData.Insert(data).ExecuteAsync();
        _logger.LogInformation("Added event data {Category}/{Key} for event {EventId}", 
            data.Category, data.Key, data.EventId);
    }

    /// <summary>
    /// Upsert a single attribute into the Event.Attributes map (creates Event row if necessary)
    /// </summary>
    public async Task UpsertAttribute(Guid userId, Guid? eventId, string? title, string key, string value)
    {
        Event? ev = null;
        if (eventId.HasValue)
        {
            var rows = await _events.Where(x => x.UserId == userId && x.Id == eventId.Value).ExecuteAsync();
            ev = rows.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(title))
        {
            var rows = await _events.Where(x => x.UserId == userId && x.Title == title).ExecuteAsync();
            ev = rows.FirstOrDefault();
        }

        if (ev == null)
        {
            ev = new Event {
                Id = eventId ?? Guid.NewGuid(),
                UserId = userId,
                Title = title ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Attributes = new Dictionary<string,string>()
            };
        }

        if (ev.Attributes == null) ev.Attributes = new Dictionary<string,string>();
        ev.Attributes[key] = value;
        ev.UpdatedAt = DateTime.UtcNow;
        if (ev.CreatedAt == default)
        {
            ev.CreatedAt = DateTime.UtcNow;
        }
        await _events.Insert(ev).ExecuteAsync();
    }

    /// <summary>
    /// Get attributes for an event by its GUID
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByEventId(Guid userId, Guid eventId)
    {
        var rows = await _events.Where(p => p.UserId == userId && p.Id == eventId).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get attributes for an event by title
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByTitle(Guid userId, string title)
    {
        var rows = await _events.Where(p => p.UserId == userId && p.Title == title).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get all flexible attributes for an event
    /// </summary>
    public async Task<IEnumerable<EventData>> GetEventData(Guid userId, Guid eventId)
    {
        return await _eventData
            .Where(ed => ed.UserId == userId && ed.EventId == eventId)
            .ExecuteAsync();
    }

    /// <summary>
    /// Ensure event-related schema exists and is configured. Called by centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        _events.CreateIfNotExists();
        _eventData.CreateIfNotExists();

        TryEnsureLcs("event");
        TryEnsureLcs("event_data");
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

    /// <summary>
    /// Get all participants (target entities) for an event
    /// </summary>
    public async Task<List<Guid>> GetParticipants(Guid userId, Guid eventId)
    {
        var evt = await GetEventById(userId, eventId);
        if (evt == null)
            return new List<Guid>();

        // Return the target entity ID (in a more complex system, this might query a separate participants table)
        var participants = new List<Guid>();
        if (evt.TargetEntityId != Guid.Empty)
        {
            participants.Add(evt.TargetEntityId);
        }

        return participants;
    }
}
