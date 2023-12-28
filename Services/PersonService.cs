using Cassandra.Data.Linq;
using Cassandra.Mapping;

namespace Coflnet.Connections.Services;

public class PersonService
{
    Cassandra.ISession session;
    Table<PersonData> personData;

    public PersonService(Cassandra.ISession session)
    {
        this.session = session;
        var mapping = new MappingConfiguration()
            .Define(new Map<PersonData>()
            .PartitionKey(t => t.UserId)
            .ClusteringKey(t => t.Name)
            .ClusteringKey(t => t.Birthday)
            .ClusteringKey(t => t.BirthPlace)
            .ClusteringKey(t => t.Category)
            .ClusteringKey(t => t.Key)
        .Column(o => o.Birthday, c => c.WithName("birthday").WithDbType<DateTime>()));
        personData = new Table<PersonData>(session, mapping);
        personData.CreateIfNotExists();
    }

    public async Task<IEnumerable<PersonData>> GetPersonData(string userId, string name)
    {
        return await personData.Where(x => x.UserId == userId && x.Name == name).ExecuteAsync();
    }

    public async Task AddPersonData(PersonData data)
    {
        await personData.Insert(data).ExecuteAsync();
    }
}