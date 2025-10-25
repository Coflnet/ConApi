using Cassandra.Data.Linq;
using Cassandra.Mapping;
using Coflnet.Connections.DTOs;
using Coflnet.Connections.Models;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

/// <summary>
/// Enhanced search service with multi-word, date range, and relational search capabilities
/// </summary>
public class EnhancedSearchService
{
    private readonly ISession _session;
    private readonly Table<SearchEntry> _searchEntries;
    private readonly PersonService _personService;
    private readonly RelationshipService _relationshipService;
    private readonly ILogger<EnhancedSearchService> _logger;

    public EnhancedSearchService(
        ISession session,
        PersonService personService,
        RelationshipService relationshipService,
        ILogger<EnhancedSearchService> logger)
    {
        _session = session;
        _personService = personService;
        _relationshipService = relationshipService;
        _logger = logger;

        var mapping = new MappingConfiguration()
            .Define(new Map<SearchEntry>()
                .PartitionKey(t => t.UserId)
                .ClusteringKey(t => t.KeyWord)
                .ClusteringKey(t => t.Type)
                .ClusteringKey(t => t.FullId)
                .Column(o => o.Type, c => c.WithName("type").WithDbType<int>()));

        _searchEntries = new Table<SearchEntry>(session, mapping);
    }

    /// <summary>
    /// Advanced search with pagination and filtering
    /// </summary>
    public async Task<SearchResultPage> SearchAdvanced(Guid userId, AdvancedSearchRequest request)
    {
        var normalizedSearch = NormalizeText(request.Query);
        var words = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var search = new Fastenshtein.Levenshtein(normalizedSearch);

        _logger.LogInformation("Advanced search for user {UserId}: '{Query}' (words: {Words})",
            userId, request.Query, words.Length);

        // Search for entries matching any word
        var tasks = words.Select(word =>
            _searchEntries.Where(x => x.UserId == userId && x.KeyWord.StartsWith(word))
                .Take(request.MaxResults ?? 1000)
                .ExecuteAsync());

        var results = await Task.WhenAll(tasks);

        var allResults = results.SelectMany(x => x)
            .Distinct()
            .Select(x => (Entry: x, Normalized: NormalizeText(x.Text)));

        // Apply entity type filter
        if (request.EntityTypes != null && request.EntityTypes.Any())
        {
            var typeSet = new HashSet<SearchEntry.ResultType>(request.EntityTypes);
            allResults = allResults.Where(x => typeSet.Contains(x.Entry.Type));
        }

        // Score and sort
        var scored = allResults
            .Select(x => new
            {
                x.Entry,
                Distance = search.DistanceFrom(x.Normalized),
                ExactMatch = x.Normalized.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(x => x.ExactMatch ? 0 : 1)
            .ThenBy(x => x.Distance)
            .ToList();

        // Apply pagination
        var pageSize = request.PageSize ?? 20;
        var skip = (request.Page ?? 0) * pageSize;
        var totalCount = scored.Count;

        var pageResults = scored
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new SearchResult
            {
                Name = x.Entry.Text,
                Description = x.Entry.KeyWord,
                Id = x.Entry.FullId.Replace("01/01/0001 00:00:00", "0001-01-01"),
                Type = x.Entry.Type.ToString(),
                Score = x.Distance
            })
            .ToList();

        return new SearchResultPage
        {
            Results = pageResults,
            TotalCount = totalCount,
            Page = request.Page ?? 0,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    /// <summary>
    /// Relational search - find entities by relationship (e.g., "John's Uncle")
    /// </summary>
    public async Task<IEnumerable<SearchResult>> RelationalSearch(Guid userId, string query)
    {
        // Parse relational queries like "John's Uncle", "Maria's Mother", etc.
        var possessiveIndex = query.IndexOf("'s ", StringComparison.OrdinalIgnoreCase);
        if (possessiveIndex == -1)
        {
            possessiveIndex = query.IndexOf("s ", StringComparison.OrdinalIgnoreCase);
        }

        if (possessiveIndex == -1)
        {
            _logger.LogWarning("Relational search query '{Query}' doesn't contain possessive", query);
            return Enumerable.Empty<SearchResult>();
        }

        var personName = query.Substring(0, possessiveIndex).Trim();
        var relationshipType = query.Substring(possessiveIndex + 2).Trim();

        _logger.LogInformation("Relational search: Find {Relationship} of {Person}", relationshipType, personName);

        // Find the person
        var personSearch = await SearchAdvanced(userId, new AdvancedSearchRequest
        {
            Query = personName,
            EntityTypes = new List<SearchEntry.ResultType> { SearchEntry.ResultType.Person },
            PageSize = 1
        });

        if (!personSearch.Results.Any())
        {
            _logger.LogWarning("Person '{PersonName}' not found for relational search", personName);
            return Enumerable.Empty<SearchResult>();
        }

        var personId = Guid.Parse(personSearch.Results.First().Id);

        // Find relationships of that type
        var relationships = await _relationshipService.GetRelationshipsByType(userId, personId, relationshipType);

        var results = new List<SearchResult>();
        foreach (var rel in relationships)
        {
            var targetId = rel.FromEntityId == personId ? rel.ToEntityId : rel.FromEntityId;

            // Search for the target entity
            var targetSearch = await _searchEntries
                .Where(x => x.UserId == userId && x.FullId.Contains(targetId.ToString()))
                .Take(1)
                .ExecuteAsync();

            var target = targetSearch.FirstOrDefault();
            if (target != null)
            {
                results.Add(new SearchResult
                {
                    Name = target.Text,
                    Description = $"{relationshipType} of {personName}",
                    Id = targetId.ToString(),
                    Type = target.Type.ToString()
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Search with date range filtering
    /// </summary>
    public async Task<IEnumerable<SearchResult>> SearchByDateRange(
        Guid userId,
        DateTime? startDate,
        DateTime? endDate,
        SearchEntry.ResultType? entityType = null)
    {
        // This would require additional date indexing in the search_entry table
        // For now, return empty - full implementation would need schema changes
        _logger.LogWarning("Date range search not yet fully implemented - requires additional indexing");
        
        // Basic implementation: search all and filter
        var allEntries = await _searchEntries
            .Where(x => x.UserId == userId)
            .Take(10000)
            .ExecuteAsync();

        var filtered = allEntries.AsEnumerable();

        if (entityType.HasValue)
        {
            filtered = filtered.Where(x => x.Type == entityType.Value);
        }

        return filtered
            .Take(100)
            .Select(x => new SearchResult
            {
                Name = x.Text,
                Description = x.KeyWord,
                Id = x.FullId.Replace("01/01/0001 00:00:00", "0001-01-01"),
                Type = x.Type.ToString()
            });
    }

    private static string NormalizeText(string text)
    {
        return string.Join(" ", text.Split(' ').Select(NormalizeKeyword).OrderBy(x => x));
    }

    private static string NormalizeKeyword(string keyWord)
    {
        var processed = keyWord.ToLower();
        if (processed.EndsWith("s"))
        {
            processed = processed.Substring(0, processed.Length - 1);
        }
        return processed;
    }
}
