using Cassandra.Data.Linq;
using Cassandra.Mapping;

namespace Coflnet.Connections.Services;
public class SearchService
{
    Cassandra.ISession session;
    Table<SearchEntry> searchEntries;

    public SearchService(Cassandra.ISession session)
    {
        this.session = session;
        var mapping = new MappingConfiguration()
            .Define(new Map<SearchEntry>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.KeyWord)
            .ClusteringKey(t => t.Type)
            .ClusteringKey(t => t.FullId)
        .Column(o => o.Type, c => c.WithName("type").WithDbType<int>()));
        searchEntries = new Table<SearchEntry>(session, mapping);
        searchEntries.CreateIfNotExists();
    }

    public async Task<IEnumerable<SearchResult>> Search(string userId, string value)
    {
        var normalizedSearch = NormalizeText(value);
        var words = normalizedSearch.Split(' ');
        var search = new Fastenshtein.Levenshtein(normalizedSearch);
        var result = await Task.WhenAll(words.Select(x => searchEntries.Where(x => x.UserId == userId && x.KeyWord.StartsWith(words[0])).Take(1000).ExecuteAsync()));
        var data = await searchEntries.Where(x => x.UserId == userId && x.KeyWord.StartsWith(words[0])).Take(1000).ExecuteAsync();
        return result.SelectMany(x => x)
            .Select(x=>(x,NormalizeText(x.Text)))
            .OrderBy(v=>search.DistanceFrom(v.Item2))
            .Take(10)
            .Select(x=>x.x)
            .Select(x => new SearchResult
            {
                Name = x.Text,
                Description = x.KeyWord,
                Id = x.FullId
            });
    }
    public async Task AddEntry(string userId, string text, string fullId, SearchEntry.ResultType type = SearchEntry.ResultType.Unknown)
    {
        var normalized = NormalizeText(text);
        foreach (var word in normalized.Split(' '))
        {
            await AddEntry(new SearchEntry
            {
                UserId = userId,
                KeyWord = word,
                FullId = fullId,
                Type = type,
                Text = text
            });
        }
    }

    public async Task AddEntry(SearchEntry entry)
    {
        entry.KeyWord = NormalizeKeyword(entry.KeyWord);
        await searchEntries.Insert(entry).ExecuteAsync();
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
