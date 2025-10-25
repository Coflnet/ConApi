using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;
public class SearchService
{
    ISession session;
    Table<SearchEntry> searchEntries;

    public SearchService(ISession session)
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
    }

    /// <summary>
    /// Ensure search schema exists and is configured. Called by centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        searchEntries.CreateIfNotExists();
        TryEnsureLcs("search_entry");
    }

    private void TryEnsureLcs(string tableName)
    {
        // Wait briefly for table metadata to appear in system_schema (schema propagation) before attempting ALTER.
        try
        {
            var keyspace = session?.Keyspace;
            if (string.IsNullOrEmpty(keyspace))
            {
                Console.WriteLine($"Session has no keyspace; skipping LCS check for {tableName}");
                return;
            }

            if (session == null)
            {
                Console.WriteLine($"No session available when checking compaction for {tableName}");
                return;
            }

            var select = new global::Cassandra.SimpleStatement(
                "SELECT compaction FROM system_schema.tables WHERE keyspace_name = ? AND table_name = ?",
                keyspace, tableName);

            var row = (global::Cassandra.Row?)null;
            for (int i = 0; i < 6; i++)
            {
                var rs = session.Execute(select);
                row = rs.FirstOrDefault();
                if (row != null) break;
                System.Threading.Thread.Sleep(500);
            }

            if (row == null)
            {
                Console.WriteLine($"Table {tableName} not yet visible in system_schema; skipping LCS");
                return;
            }

            try
            {
                var compaction = row.GetValue<IDictionary<string, string>>("compaction");
                if (compaction != null && compaction.TryGetValue("class", out var cls) &&
                    !string.IsNullOrEmpty(cls) && cls.Contains("LeveledCompactionStrategy", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Table {tableName} already uses compaction {cls}");
                    return;
                }
            }
            catch { }

            var target = $"{keyspace}.{tableName}";
            var cql = $"ALTER TABLE {target} WITH compaction = {{'class':'LeveledCompactionStrategy'}}";
            session.Execute(new global::Cassandra.SimpleStatement(cql));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"EnsureLcs failed for {tableName}: {ex.Message}");
        }
    }

    public async Task<IEnumerable<SearchResult>> Search(Guid userId, string value)
    {
        var normalizedSearch = NormalizeText(value);
        var words = normalizedSearch.Split(' ');
        var search = new Fastenshtein.Levenshtein(normalizedSearch);
        
        // Search for entries starting with each word
        var result = await Task.WhenAll(words.Select(word => 
            searchEntries.Where(x => x.UserId == userId && x.KeyWord.StartsWith(word))
                .Take(1000)
                .ExecuteAsync()));
        
        return result.SelectMany(x => x)
            .Distinct()
            .Select(x=>(x,NormalizeText(x.Text)))
            .OrderBy(v=>search.DistanceFrom(v.Item2))
            .Take(10)
            .Select(x=>x.x)
            .Select(x => new SearchResult
            {
                Name = x.Text,
                Description = x.KeyWord,
                Id = x.FullId.Replace("01/01/0001 00:00:00", "0001-01-01"),
                Type = x.Type.ToString()
            });
    }
    public async Task AddEntry(Guid userId, string text, string fullId, SearchEntry.ResultType type = SearchEntry.ResultType.Unknown)
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
            Console.WriteLine($"Add search entry {word}->{fullId}");
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
