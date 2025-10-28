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
        
        _events = new Table<Event>(session, GlobalMapping.Instance);
        _eventData = new Table<EventData>(session, GlobalMapping.Instance);
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
        
        // With the new partitioning by (user_id, event_year, target_entity_id) we query per-year
        var collected = new List<Event>();
        // derive year bounds from requested start/end or default to current year
        int startYear = (startDate ?? DateTime.UtcNow).Year;
        int endYear = (endDate ?? DateTime.UtcNow).Year;

        // If requested range is large, constrain to +/-20 years around startYear
        int minYear = Math.Min(startYear, endYear);
        int maxYear = Math.Max(startYear, endYear);

        // Expand the search window up to 20 years if needed
        int yearsChecked = 0;
        // We'll search ascending from minYear to maxYear, then expand outward until we've checked up to 20 years
        var yearsToCheck = new HashSet<int>();
        for (int y = minYear; y <= maxYear; y++) yearsToCheck.Add(y);

        int left = minYear - 1;
        int right = maxYear + 1;
        while (yearsChecked < 20 && yearsToCheck.Count < 20)
        {
            if (left >= minYear - 20) { yearsToCheck.Add(left); left--; }
            if (yearsToCheck.Count >= 20) break;
            if (right <= maxYear + 20) { yearsToCheck.Add(right); right++; }
            yearsChecked = yearsToCheck.Count;
        }

        // For each year, read the partition for (userId, year, targetEntityId)
        foreach (var year in yearsToCheck.OrderBy(y => y))
        {
            try
            {
                var rows = await _events
                    .Where(e => e.UserId == userId && e.EventYear == year && e.TargetEntityId == targetEntityId)
                    .Take(1000)
                    .ExecuteAsync();
                collected.AddRange(rows);
                // If we already have events covering the requested date range, stop early
                if (startDate.HasValue || endDate.HasValue)
                {
                    var within = collected.Where(e => (!startDate.HasValue || e.EventDate >= startDate.Value) && (!endDate.HasValue || e.EventDate <= endDate.Value));
                    if (within.Any()) break;
                }
                else
                {
                    if (collected.Any()) break;
                }
            }
            catch (Exception ex)
            {
                // if a prepare error happens for a year (schema not yet visible), refresh metadata and retry next year
                _logger.LogDebug(ex, "Error querying events for year {Year}; continuing with next year", year);
            }
        }

        var allEvents = collected;

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
        // Ensure EventYear is set from EventDate for partitioning
        if (ev.EventDate != default)
        {
            ev.EventYear = ev.EventDate.Year;
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
        try
        {
            var ksNullable = _session?.Keyspace;
            if (string.IsNullOrEmpty(ksNullable)) throw new InvalidOperationException("Session has no keyspace set");
            var ks = ksNullable!;

            var createEvent = @"CREATE TABLE IF NOT EXISTS event (
                user_id uuid,
                title text,
                event_year int,
                event_date timestamp,
                target_entity_id uuid,
                end_date timestamp,
                type int,
                target_entity_type int,
                privacy_level int,
                attributes map<text,text>,
                description text,
                place_id uuid,
                id uuid,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY ((user_id, event_year, target_entity_id), event_date, id)
            );";

            var createEventData = @"CREATE TABLE IF NOT EXISTS event_data (
                user_id uuid,
                event_id uuid,
                category text,
                key text,
                value text,
                changed_at timestamp,
                PRIMARY KEY ((user_id, event_id), category, key)
            );";

            if (_session != null)
            {
                _session.Execute(new SimpleStatement(createEvent.Replace("CREATE TABLE IF NOT EXISTS ", $"CREATE TABLE IF NOT EXISTS {ks}.")));
                _session.Execute(new SimpleStatement(createEventData.Replace("CREATE TABLE IF NOT EXISTS ", $"CREATE TABLE IF NOT EXISTS {ks}.")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create event tables via CQL in EnsureSchema(); falling back to driver CreateIfNotExists()");
            try
            {
                _events.CreateIfNotExists();
                _eventData.CreateIfNotExists();
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Fallback CreateIfNotExists failed for event schema");
            }
        }

        TryEnsureLcs("event");
        TryEnsureLcs("event_data");

        // Ensure flexible attributes column exists for events
        if (_session != null)
        {
            SchemaHelper.TryEnsureColumn(_session, _logger, "event", "attributes", "map<text,text>");
            // Ensure end_date column exists (used by Event.EndDate mapping)
            SchemaHelper.TryEnsureColumn(_session, _logger, "event", "end_date", "timestamp");
            // Ensure event_year exists
            SchemaHelper.TryEnsureColumn(_session, _logger, "event", "event_year", "int");
        }
    }

    private void TryEnsureLcs(string tableName)
    {
        SchemaHelper.TryEnsureLcs(_session, _logger, tableName);
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
