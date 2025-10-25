using Microsoft.Extensions.Caching.Memory;

namespace Coflnet.Connections.Services;

/// <summary>
/// Caching service for frequently accessed data
/// </summary>
public class CachingService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingService> _logger;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

    public CachingService(IMemoryCache cache, ILogger<CachingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get or create cached item
    /// </summary>
    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);

        var value = await factory();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _defaultExpiration
        };

        _cache.Set(key, value, cacheOptions);

        return value;
    }

    /// <summary>
    /// Invalidate cache entry
    /// </summary>
    public void Invalidate(string key)
    {
        _cache.Remove(key);
        _logger.LogDebug("Cache invalidated for key: {Key}", key);
    }

    /// <summary>
    /// Invalidate all cache entries matching a pattern
    /// </summary>
    public void InvalidatePattern(string pattern)
    {
        // Note: IMemoryCache doesn't support pattern matching out of the box
        // In production, consider using Redis with SCAN command
        _logger.LogWarning("Pattern invalidation not fully supported with IMemoryCache. Pattern: {Pattern}", pattern);
    }

    /// <summary>
    /// Generate cache key for person
    /// </summary>
    public static string PersonKey(Guid userId, Guid personId) => $"person:{userId}:{personId}";

    /// <summary>
    /// Generate cache key for place
    /// </summary>
    public static string PlaceKey(Guid userId, Guid placeId) => $"place:{userId}:{placeId}";

    /// <summary>
    /// Generate cache key for thing
    /// </summary>
    public static string ThingKey(Guid userId, Guid thingId) => $"thing:{userId}:{thingId}";

    /// <summary>
    /// Generate cache key for event
    /// </summary>
    public static string EventKey(Guid userId, Guid eventId) => $"event:{userId}:{eventId}";

    /// <summary>
    /// Generate cache key for relationships
    /// </summary>
    public static string RelationshipsKey(Guid userId, Guid entityId) => $"relationships:{userId}:{entityId}";

    /// <summary>
    /// Generate cache key for search results
    /// </summary>
    public static string SearchKey(Guid userId, string query) => $"search:{userId}:{query.ToLowerInvariant()}";

    /// <summary>
    /// Generate cache key for full person view
    /// </summary>
    public static string PersonFullKey(Guid userId, Guid personId) => $"person_full:{userId}:{personId}";

    /// <summary>
    /// Generate cache key for person timeline
    /// </summary>
    public static string PersonTimelineKey(Guid userId, Guid personId) => $"person_timeline:{userId}:{personId}";
}
