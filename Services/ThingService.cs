using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing things (physical objects)
/// </summary>
public class ThingService
{
    private readonly ISession _session;
    private readonly Table<Thing> _things;
    private readonly Table<ThingData> _thingData;
    private readonly Table<ThingByOwner> _thingsByOwner;
    private readonly ILogger<ThingService> _logger;
    private readonly SearchService _searchService;

    public ThingService(ISession session, ILogger<ThingService> logger, SearchService searchService)
    {
        _session = session;
        _logger = logger;
        _searchService = searchService;

        _things = new Table<Thing>(session, GlobalMapping.Instance);
        _thingData = new Table<ThingData>(session, GlobalMapping.Instance);
        _thingsByOwner = new Table<ThingByOwner>(session, GlobalMapping.Instance);
    }

    /// <summary>
    /// Ensure thing-related schema exists and is configured. This is invoked by the centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        try
        {
            var ksNullable = _session?.Keyspace;
            if (string.IsNullOrEmpty(ksNullable)) throw new InvalidOperationException("Session has no keyspace set");
            var ks = ksNullable!;

            var createThing = @"CREATE TABLE IF NOT EXISTS thing (
                user_id uuid,
                name text,
                id uuid,
                type int,
                owner_id uuid,
                manufacturer text,
                year_made int,
                model text,
                serial_number text,
                attributes map<text,text>,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY ((user_id, name), id)
            );";

            var createThingData = @"CREATE TABLE IF NOT EXISTS thing_data (
                user_id uuid,
                thing_id uuid,
                category text,
                key text,
                value text,
                changed_at timestamp,
                PRIMARY KEY ((user_id, thing_id), category, key)
            );";

            var createByOwner = @"CREATE TABLE IF NOT EXISTS thing_by_owner (
                user_id uuid,
                owner_id uuid,
                thing_id uuid,
                name text,
                type int,
                created_at timestamp,
                updated_at timestamp,
                PRIMARY KEY (user_id, owner_id, thing_id)
            );";

            if (_session != null)
            {
                _session.Execute(new SimpleStatement(createThing.Replace("CREATE TABLE IF NOT EXISTS ", $"CREATE TABLE IF NOT EXISTS {ks}.")));
                _session.Execute(new SimpleStatement(createThingData.Replace("CREATE TABLE IF NOT EXISTS ", $"CREATE TABLE IF NOT EXISTS {ks}.")));
                _session.Execute(new SimpleStatement(createByOwner.Replace("CREATE TABLE IF NOT EXISTS ", $"CREATE TABLE IF NOT EXISTS {ks}.")));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create thing tables via CQL in EnsureSchema(); will fall back to driver CreateIfNotExists().");
            try
            {
                // The Table<T>.CreateIfNotExists is idempotent and safe to call multiple times.
                _things.CreateIfNotExists();
                _thingData.CreateIfNotExists();
                _thingsByOwner.CreateIfNotExists();
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Fallback Table<T>.CreateIfNotExists() also failed in EnsureSchema()");
            }
        }

        // Prefer Leveled Compaction Strategy for read-heavy tables.
        TryEnsureLcs("thing");
        TryEnsureLcs("thing_data");
        TryEnsureLcs("thing_by_owner");
    }

    private void TryEnsureLcs(string tableName)
    {
        SchemaHelper.TryEnsureLcs(_session, _logger, tableName);
    }

    /// <summary>
    /// Get a thing by ID
    /// </summary>
    public async Task<Thing?> GetThingById(Guid userId, Guid thingId)
    {
        var result = await _things
            .Where(t => t.UserId == userId && t.Id == thingId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Get things by name
    /// </summary>
    public async Task<IEnumerable<Thing>> GetThingsByName(Guid userId, string name)
    {
        return await _things
            .Where(t => t.UserId == userId && t.Name == name)
            .ExecuteAsync();
    }

    /// <summary>
    /// Read-optimized: query things by owner using denormalized table
    /// </summary>
    public async Task<IEnumerable<Thing>> GetThingsByOwner(Guid userId, Guid ownerId)
    {
        var rows = await _thingsByOwner
            .Where(r => r.UserId == userId && r.OwnerId == ownerId)
            .ExecuteAsync();

        var result = new List<Thing>();
        foreach (var row in rows)
        {
            var t = await GetThingById(userId, row.ThingId);
            if (t != null) result.Add(t);
        }

        return result;
    }

    /// <summary>
    /// Add or update a thing
    /// </summary>
    public async Task<Thing> SaveThing(Thing thing)
    {
        if (thing.Id == Guid.Empty)
        {
            thing.Id = Guid.NewGuid();
        }

        thing.UpdatedAt = DateTime.UtcNow;
        if (thing.CreatedAt == default)
        {
            thing.CreatedAt = DateTime.UtcNow;
        }

        await _things.Insert(thing).ExecuteAsync();

        // maintain denormalized table for reads
        if (thing.OwnerId.HasValue)
        {
            var bo = new ThingByOwner
            {
                UserId = thing.UserId,
                OwnerId = thing.OwnerId.Value,
                ThingId = thing.Id,
                Name = thing.Name,
                Type = thing.Type,
                CreatedAt = thing.CreatedAt,
                UpdatedAt = thing.UpdatedAt
            };

            await _thingsByOwner.Insert(bo).ExecuteAsync();
        }

        _logger.LogInformation("Saved thing {ThingId} '{ThingName}' for user {UserId}", 
            thing.Id, thing.Name, thing.UserId);

        return thing;
    }

    /// <summary>
    /// Add flexible attribute to a thing
    /// </summary>
    public async Task AddThingData(ThingData data)
    {
        data.ChangedAt = DateTime.UtcNow;
        await _thingData.Insert(data).ExecuteAsync();
        _logger.LogInformation("Added thing data {Category}/{Key} for thing {ThingId}", 
            data.Category, data.Key, data.ThingId);
    }

    /// <summary>
    /// Upsert a single attribute into the Thing.Attributes map (creates Thing row if necessary)
    /// </summary>
    public async Task UpsertAttribute(Guid userId, Guid? thingId, string? name, string key, string value)
    {
        Thing? t = null;
        if (thingId.HasValue)
        {
            var rows = await _things.Where(x => x.UserId == userId && x.Id == thingId.Value).ExecuteAsync();
            t = rows.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(name))
        {
            var rows = await _things.Where(x => x.UserId == userId && x.Name == name).ExecuteAsync();
            t = rows.FirstOrDefault();
        }

        if (t == null)
        {
            t = new Thing {
                Id = thingId ?? Guid.NewGuid(),
                UserId = userId,
                Name = name ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Attributes = new Dictionary<string,string>()
            };
        }

        if (t.Attributes == null) t.Attributes = new Dictionary<string,string>();
        t.Attributes[key] = value;
        t.UpdatedAt = DateTime.UtcNow;
        if (t.CreatedAt == default)
        {
            t.CreatedAt = DateTime.UtcNow;
        }
        await _things.Insert(t).ExecuteAsync();

        try
        {
            if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(t.Name))
            {
                await _searchService.AddEntry(userId, t.Name, t.Id.ToString(), SearchEntry.ResultType.Unknown);
            }
        }
        catch { }
    }

    /// <summary>
    /// Get attributes for a thing by its GUID
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByThingId(Guid userId, Guid thingId)
    {
        var rows = await _things.Where(p => p.UserId == userId && p.Id == thingId).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get attributes for a thing by name
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByName(Guid userId, string name)
    {
        var rows = await _things.Where(p => p.UserId == userId && p.Name == name).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get all flexible attributes for a thing
    /// </summary>
    public async Task<IEnumerable<ThingData>> GetThingData(Guid userId, Guid thingId)
    {
        return await _thingData
            .Where(td => td.UserId == userId && td.ThingId == thingId)
            .ExecuteAsync();
    }

    /// <summary>
    /// Get specific attribute category for a thing
    /// </summary>
    public async Task<IEnumerable<ThingData>> GetThingData(Guid userId, Guid thingId, string category)
    {
        return await _thingData
            .Where(td => td.UserId == userId && td.ThingId == thingId && td.Category == category)
            .ExecuteAsync();
    }
}
