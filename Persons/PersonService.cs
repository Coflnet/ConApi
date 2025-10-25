using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;
using Coflnet.Connections;

namespace Coflnet.Connections.Services;

public class PersonService
{
    ISession session;
    Table<Person> persons;
    private readonly SearchService _searchService;

    public PersonService(ISession session, SearchService searchService)
    {
        this.session = session;
        _searchService = searchService;
        var mapping = new MappingConfiguration()
            .Define(new Map<Person>()
                .PartitionKey(t => t.UserId, t => t.Name)
                .ClusteringKey(t => t.Birthday)
                .ClusteringKey(t => t.BirthPlace)
                .Column(t => t.Gender, c => c.WithName("gender").WithDbType<int>())
                .Column(t => t.PrivacyLevel, c => c.WithName("privacy_level").WithDbType<int>())
            );
        persons = new Table<Person>(session, mapping);
    }

    /// <summary>
    /// Get flexible attributes for a person identified by name (legacy composite key)
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByName(Guid userId, string name)
    {
        var rows = await persons.Where(p => p.UserId == userId && p.Name == name).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Get flexible attributes by personId (GUID)
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByPersonId(Guid userId, Guid personId)
    {
        var rows = await persons.Where(p => p.UserId == userId && p.Id == personId).ExecuteAsync();
        var first = rows.FirstOrDefault();
        return first?.Attributes ?? new Dictionary<string,string>();
    }

    /// <summary>
    /// Upsert a single attribute into the Person.Attributes map (creates Person row if necessary)
    /// </summary>
    public async Task UpsertAttribute(Guid userId, Guid? personId, string name, string key, string value)
    {
        Person? p = null;
        if (personId.HasValue)
        {
            var rows = await persons.Where(x => x.UserId == userId && x.Id == personId.Value).ExecuteAsync();
            p = rows.FirstOrDefault();
        }
        else if (!string.IsNullOrEmpty(name))
        {
            var rows = await persons.Where(x => x.UserId == userId && x.Name == name).ExecuteAsync();
            p = rows.FirstOrDefault();
        }

        if (p == null)
        {
            p = new Person {
                Id = personId ?? Guid.NewGuid(),
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
        // If this row already existed (has non-empty CreatedAt) update, otherwise insert
        // In Cassandra INSERT acts as an upsert (writes provided columns). Use Insert to upsert the person row.
        if (p.CreatedAt == default)
        {
            p.CreatedAt = DateTime.UtcNow;
        }
        await persons.Insert(p).ExecuteAsync();

        // If the attribute updated is 'name', update search index
        try
        {
            if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.Name))
            {
                await _searchService.AddEntry(userId, p.Name, p.Id.ToString(), SearchEntry.ResultType.Person);
            }
        }
        catch { }
    }

    /// <summary>
    /// Ensure person table exists and compaction is configured. Called by centralized migration runner.
    /// </summary>
    public void EnsureSchema()
    {
        persons.CreateIfNotExists();
        TryEnsureLcs("person");
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

            // Poll for the table to appear in system_schema (schema propagation can be slightly delayed)
            var row = (global::Cassandra.Row?)null;
            for (int i = 0; i < 6; i++) // try ~3 seconds total (6 * 500ms)
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
}
