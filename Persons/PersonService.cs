using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ISession = Cassandra.ISession;

namespace Coflnet.Connections.Services;

public class PersonService
{
    ISession session;
    Table<PersonData> personData;

    public PersonService(ISession session)
    {
        this.session = session;
        var mapping = new MappingConfiguration()
            .Define(new Map<PersonData>()
            .PartitionKey(t => t.UserId, t => t.Name)
            .ClusteringKey(t => t.Birthday)
            .ClusteringKey(t => t.BirthPlace)
            .ClusteringKey(t => t.Category)
            .ClusteringKey(t => t.Key)
        .Column(o => o.Birthday, c => c.WithName("birthday").WithDbType<DateTime>()));
        personData = new Table<PersonData>(session, mapping);
        personData.CreateIfNotExists();
    }

    public async Task<IEnumerable<PersonData>> GetPersonData(Guid userId, string name)
    {
        return await personData.Where(x => x.UserId == userId && x.Name == name).ExecuteAsync();
    }
    public async Task<IEnumerable<PersonData>> GetPersonData(Guid userId, string name, DateTime birthday)
    {
        return await personData.Where(x => x.UserId == userId && x.Name == name && x.Birthday == birthday).ExecuteAsync();
    }
    public async Task<IEnumerable<PersonData>> GetPersonData(Guid userId, string name, DateTime birthday, string birthPlace)
    {
        return await personData.Where(x => x.UserId == userId &&  x.Name == name && x.Birthday == birthday && x.BirthPlace == birthPlace).ExecuteAsync();
    }

    public async Task AddPersonData(PersonData data)
    {
        await personData.Insert(data).ExecuteAsync();
    }
}
