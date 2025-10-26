using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;
using Coflnet.Connections;
using System.Linq;

namespace Coflnet.Connections.Services;

public class PersonService
{
    ISession session;
    Table<Person> persons;
    private readonly SearchService _searchService;
    private readonly ISession _sessionInternal;

    public PersonService(ISession session, SearchService searchService)
    {
        this.session = session;
        _searchService = searchService;
        persons = new Table<Person>(session, GlobalMapping.Instance);
        // Use session directly for person_data fallback operations
        _sessionInternal = session;
    }

    /// <summary>
    /// Get flexible attributes for a person identified by name (legacy composite key) — now deprecated.
    /// For backward compatibility, reads from person_data fallback table by name.
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByName(Guid userId, string name)
    {
        try
        {
            var keyspace = _sessionInternal.Keyspace;
            if (!string.IsNullOrEmpty(keyspace))
            {
                var cql = $"SELECT key, value FROM {keyspace}.person_data WHERE user_id = ? AND name = ?";
                var rs = await _sessionInternal.ExecuteAsync(new SimpleStatement(cql, userId, name));
                return rs.ToDictionary(r => r.GetValue<string>("key"), r => r.GetValue<string>("value"));
            }
        }
        catch { }
        return new Dictionary<string, string>();
    }

    /// <summary>
    /// Get flexible attributes by personId (GUID)
    /// </summary>
    public async Task<IDictionary<string,string>> GetAttributesByPersonId(Guid userId, Guid personId)
    {
        try
        {
            // Direct lookup by userId and personId (no filtering needed)
            var rows = await persons.Where(p => p.UserId == userId && p.Id == personId).ExecuteAsync();
            var first = rows.FirstOrDefault();
            var result = first?.Attributes ?? new Dictionary<string, string>();

            // Also merge any fallback person_data rows (raw CQL)
            try
            {
                var keyspace = _sessionInternal.Keyspace;
                if (!string.IsNullOrEmpty(keyspace))
                {
                    var cql = $"SELECT key, value FROM {keyspace}.person_data WHERE user_id = ? AND person_id = ?";
                    var rs = await _sessionInternal.ExecuteAsync(new SimpleStatement(cql, userId, personId));
                    foreach (var row in rs)
                    {
                        var k = row.GetValue<string>("key");
                        var v = row.GetValue<string>("value");
                        if (!result.ContainsKey(k)) result[k] = v;
                    }
                }
            }
            catch { }

            return result;
        }
        catch (InvalidQueryException)
        {
            // Fallback to PersonData table if person row can't be read
            var keyspace = _sessionInternal.Keyspace;
            if (!string.IsNullOrEmpty(keyspace))
            {
                var cql = $"SELECT key, value FROM {keyspace}.person_data WHERE user_id = ? AND person_id = ?";
                var rs = await _sessionInternal.ExecuteAsync(new SimpleStatement(cql, userId, personId));
                return rs.ToDictionary(r => r.GetValue<string>("key"), r => r.GetValue<string>("value"));
            }
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Upsert a single attribute into the Person.Attributes map (creates Person row if necessary)
    /// </summary>
    public async Task UpsertAttribute(Guid userId, Guid? personId, string name, string key, string value)
    {
        await UpsertAttributeInternal(userId, personId, name, key, value);
    }

    private async Task UpsertAttributeInternal(Guid userId, Guid? personId, string name, string key, string value)
    {
        Person? p = null;
        if (personId.HasValue)
        {
            // Direct lookup by userId and personId (UUID-based)
            var rows = await persons.Where(x => x.UserId == userId && x.Id == personId.Value).ExecuteAsync();
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
        if (p.CreatedAt == default)
        {
            p.CreatedAt = DateTime.UtcNow;
        }
        try
        {
            await persons.Insert(p).ExecuteAsync();
        }
        catch (InvalidQueryException)
        {
            // Could be a marshaling error for the Attributes map column (schema mismatch).
            // Fallback: persist the single attribute into person_data table to avoid losing the update.
            try
            {
                var keyspace = _sessionInternal.Keyspace;
                if (!string.IsNullOrEmpty(keyspace))
                {
                    var cql = $"INSERT INTO {keyspace}.person_data (user_id, person_id, name, key, value, changed_at) VALUES (?, ?, ?, ?, ?, toTimestamp(now()))";
                    var nameToUse = p.Name ?? string.Empty;
                    await _sessionInternal.ExecuteAsync(new SimpleStatement(cql, p.UserId, p.Id, nameToUse, key, value));
                }
            }
            catch { }
        }

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
        // Ensure fallback table person_data exists so we can persist single attributes
        try
        {
            var keyspace = session?.Keyspace;
            var s = session;
            if (s != null && !string.IsNullOrEmpty(keyspace))
            {
                var create = $"CREATE TABLE IF NOT EXISTS {keyspace}.person_data (" +
                             "user_id uuid, " +
                             "person_id uuid, " +
                             "name text, " +
                             "key text, " +
                             "value text, " +
                             "changed_at timestamp, " +
                             "PRIMARY KEY (user_id, person_id, name, key)" +
                             ")";
                s.Execute(new global::Cassandra.SimpleStatement(create));

                // Create helpful secondary indexes so we can query by name or person_id
                var idxName = $"CREATE INDEX IF NOT EXISTS ON {keyspace}.person_data (name)";
                var idxPerson = $"CREATE INDEX IF NOT EXISTS ON {keyspace}.person_data (person_id)";
                s.Execute(new global::Cassandra.SimpleStatement(idxName));
                s.Execute(new global::Cassandra.SimpleStatement(idxPerson));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to ensure person_data table: {ex.Message}");
        }
    }

    private void TryEnsureLcs(string tableName)
    {
        SchemaHelper.TryEnsureLcs(session, null, tableName);
    }
}
