using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Service for managing places with hierarchical support
/// </summary>
public class PlaceService
{
    private readonly ISession _session;
    private readonly Table<Place> _places;
    private readonly Table<PlaceData> _placeData;
    private readonly ILogger<PlaceService> _logger;
    private readonly SearchService _searchService;

    public PlaceService(ISession session, ILogger<PlaceService> logger, SearchService searchService)
    {
        _session = session;
        _logger = logger;
        _searchService = searchService;
        
        _places = new Table<Place>(session, GlobalMapping.Instance);
        _placeData = new Table<PlaceData>(session, GlobalMapping.Instance);
    }

    /// <summary>
    /// Get a place by ID
    /// </summary>
    public async Task<Place?> GetPlaceById(Guid userId, Guid placeId)
    {
        var result = await _places
            .Where(p => p.UserId == userId && p.Id == placeId)
            .ExecuteAsync();
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Get places by name (partial match)
    /// </summary>
    public async Task<IEnumerable<Place>> GetPlacesByName(Guid userId, string name)
    {
        return await _places
            .Where(p => p.UserId == userId && p.Name == name)
            .ExecuteAsync();
    }

    /// <summary>
    /// Get child places of a parent
    /// </summary>
    public async Task<IEnumerable<Place>> GetChildPlaces(Guid userId, Guid parentPlaceId)
    {
        // Note: This requires a secondary index or materialized view in production
        // For now, we'll need to fetch and filter
        _logger.LogWarning("GetChildPlaces performs client-side filtering - consider adding materialized view");
        var allPlaces = await _places
            .Where(p => p.UserId == userId)
            .Take(10000)
            .ExecuteAsync();
        
        return allPlaces.Where(p => p.ParentPlaceId == parentPlaceId);
    }

    /// <summary>
    /// Add or update a place
    /// </summary>
    public async Task<Place> SavePlace(Place place)
    {
        if (place.Id == Guid.Empty)
        {
            place.Id = Guid.NewGuid();
        }
        
        place.UpdatedAt = DateTime.UtcNow;
        if (place.CreatedAt == default)
        {
            place.CreatedAt = DateTime.UtcNow;
        }

        // Update hierarchy path if parent exists
        if (place.ParentPlaceId.HasValue)
        {
            var parent = await GetPlaceById(place.UserId, place.ParentPlaceId.Value);
            if (parent != null)
            {
                place.HierarchyPath = string.IsNullOrEmpty(parent.HierarchyPath)
                    ? $"{parent.Name}/{place.Name}"
                    : $"{parent.HierarchyPath}/{place.Name}";
            }
        }
        else
        {
            place.HierarchyPath = place.Name;
        }
        
        await _places.Insert(place).ExecuteAsync();
        _logger.LogInformation("Saved place {PlaceId} '{PlaceName}' for user {UserId}", 
            place.Id, place.Name, place.UserId);
        
        return place;
    }

    /// <summary>
    /// Add flexible attribute to a place
    /// </summary>
    public async Task AddPlaceData(PlaceData data)
    {
        data.ChangedAt = DateTime.UtcNow;
        await _placeData.Insert(data).ExecuteAsync();
        _logger.LogInformation("Added place data {Category}/{Key} for place {PlaceId}", 
            data.Category, data.Key, data.PlaceId);
    }

    /// <summary>
    /// Upsert a single attribute into the Place.Attributes map (creates Place row if necessary)
    /// </summary>
    public async Task UpsertAttribute(Guid userId, Guid? placeId, string? name, string key, string value)
    {
        Place? p = null;
        if (placeId.HasValue)
        {
            var rows = await _places.Where(x => x.UserId == userId && x.Id == placeId.Value).ExecuteAsync();
            p = rows.FirstOrDefault();
        }

        if (p == null)
        {
            p = new Place {
                Id = placeId ?? Guid.NewGuid(),
                UserId = userId,
                Name = name ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Attributes = new Dictionary<string,string>()
            };
        }

        if (p.Attributes == null) p.Attributes = new Dictionary<string,string>();
        p.Attributes[key] = value;
        p.UpdatedAt = DateTime.UtcNow;
        if (p.CreatedAt == default)
        {
            p.CreatedAt = DateTime.UtcNow;
        }
        await _places.Insert(p).ExecuteAsync();

        // update search index if the primary name changed
        try
        {
            if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.Name))
            {
                await _searchService.AddEntry(userId, p.Name, p.Id.ToString(), SearchEntry.ResultType.Location);
            }
        }
        catch { }
    }

    /// <summary>
    /// Get attributes for a place by its GUID
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByPlaceId(Guid userId, Guid placeId)
    {
        var rows = await _places.Where(p => p.UserId == userId && p.Id == placeId).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get attributes for a place by name
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByName(Guid userId, string name)
    {
        var rows = await _places.Where(p => p.UserId == userId && p.Name == name).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get all flexible attributes for a place
    /// </summary>
    public async Task<IEnumerable<PlaceData>> GetPlaceData(Guid userId, Guid placeId)
    {
        return await _placeData
            .Where(pd => pd.UserId == userId && pd.PlaceId == placeId)
            .ExecuteAsync();
    }

    /// <summary>
    /// Get specific attribute for a place
    /// </summary>
    public async Task<IEnumerable<PlaceData>> GetPlaceData(Guid userId, Guid placeId, string category)
    {
        return await _placeData
            .Where(pd => pd.UserId == userId && pd.PlaceId == placeId && pd.Category == category)
            .ExecuteAsync();
    }

    /// <summary>
    /// Ensure place-related schema exists and is configured. Called by centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        _places.CreateIfNotExists();
        _placeData.CreateIfNotExists();

        TryEnsureLcs("place");
        TryEnsureLcs("place_data");
    }

    private void TryEnsureLcs(string tableName)
    {
        // Wait briefly for table metadata to appear in system_schema (schema propagation) before attempting ALTER.
        SchemaHelper.TryEnsureLcs(_session, _logger, tableName);
    }
}
