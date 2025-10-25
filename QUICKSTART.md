# 🚀 Quick Start Guide - Connections API

## For Developers New to This Project

### 📖 What to Read First

1. **IMPLEMENTATION_SUMMARY.md** - What was built (5 min read)
2. **README.md** - Full API documentation (15 min read)
3. **MIGRATION.md** - Frontend integration guide (10 min read)
4. **PLAN.txt** - Implementation roadmap (5 min read)

### 🎯 What This API Does

Track people, places, things, and their relationships:
- **Genealogy** - Build family trees
- **History** - Timeline of events
- **Collections** - Track belongings (cars, toys, etc.)
- **Places** - Hierarchical locations
- **Memories** - Link events to people/places/things

### 🏃 Running Locally

```bash
# 1. Prerequisites
# - .NET 8.0 SDK
# - Cassandra database
# - (Optional) Docker

# 2. Clone and build
cd ConApi
dotnet restore
dotnet build

# 3. Configure Cassandra (appsettings.json)
{
  "CASSANDRA": {
    "HOSTS": "localhost",
    "KEYSPACE": "connections"
  }
}

# 4. Run
dotnet run

# 5. Access Swagger
# https://localhost:5001/swagger
```

### 🧪 Running Tests

```bash
cd ConApi.Tests
dotnet test
# Expected: 11/11 tests passing
```

### 📡 Quick API Examples

#### Search for a Person
```bash
curl -H "Authorization: Bearer {token}" \
  "https://con.coflnet.com/api/search?value=Fritz"
```

#### Get Family Relationships
```bash
curl -H "Authorization: Bearer {token}" \
  "https://con.coflnet.com/api/relationship/entity/{personId}"
```

#### Get Timeline
```bash
curl -X POST -H "Authorization: Bearer {token}" \
  -H "Content-Type: application/json" \
  -d '{"entityType":1,"entityId":"{personId}"}' \
  "https://con.coflnet.com/api/event/timeline"
```

### 🗺️ Project Structure

```
ConApi/
├── Models/              # Entity definitions
│   ├── BaseEntity.cs    # Base class with privacy
│   ├── Person.cs        # Person entity
│   ├── Place.cs         # Place entity (hierarchical)
│   ├── Thing.cs         # Thing entity (physical objects)
│   ├── Event.cs         # Event entity (timeline)
│   └── Relationship.cs  # Relationship entity (bidirectional)
│
├── Services/            # Business logic
│   ├── PersonService.cs
│   ├── PlaceService.cs
│   ├── ThingService.cs
│   ├── EventService.cs
│   ├── RelationshipService.cs
│   └── SearchService.cs
│
├── Controllers/         # API endpoints
│   ├── PersonController.cs
│   ├── PlaceController.cs
│   ├── ThingController.cs
│   ├── EventController.cs
│   ├── RelationshipController.cs
│   └── SearchController.cs
│
├── DTOs/                # Request/Response models
│   ├── ApiResponse.cs
│   └── EntityDtos.cs
│
├── Middleware/          # Cross-cutting concerns
│   └── GlobalExceptionHandler.cs
│
├── Persons/             # Legacy person code
├── Search/              # Legacy search code
│
└── Documentation/
    ├── README.md
    ├── MIGRATION.md
    ├── PLAN.txt
    └── IMPLEMENTATION_SUMMARY.md
```

### 🔑 Key Concepts

#### 1. Entity Types
```csharp
enum EntityType {
  Person = 1,   // People
  Place = 2,    // Locations
  Thing = 3,    // Physical objects
  Event = 4     // Timeline events
}
```

#### 2. Privacy Levels
```csharp
enum PrivacyLevel {
  Private = 0,  // Only you
  Family = 1,   // Family members
  Friends = 2,  // Friends
  Public = 3    // Everyone
}
```

#### 3. Flexible Attributes
Every entity has a companion `*Data` table for flexible key-value storage:
- `PersonData`, `PlaceData`, `ThingData`, `EventData`
- Store custom fields without schema changes

#### 4. Bidirectional Relationships
Creating one relationship automatically creates the inverse:
```
Create: Hans -[Mutter]-> Maria
Auto-creates: Maria -[Kind]-> Hans
```

### 🎨 Common Patterns

#### Creating an Entity
```csharp
// 1. Create core entity
var person = new Person {
  Name = "Schmidt",
  Birthday = new DateTime(1980, 5, 15),
  // ...
};
await personService.SavePerson(person);

// 2. Add flexible attributes
await personService.AddPersonData(new PersonData {
  Category = "contact",
  Key = "email",
  Value = "hans@example.com"
});

// 3. Add to search index
await searchService.AddEntry(userId, "Hans Schmidt", person.Id.ToString());
```

#### Querying Relationships
```csharp
// Get all relationships for a person
var relationships = await relationshipService
  .GetRelationshipsForEntity(userId, personId);

// Filter by type
var mothers = relationships
  .Where(r => r.RelationshipType == "Mutter");

// Find path between two people
var path = await relationshipService
  .FindRelationshipPath(userId, personA, personB);
```

#### Building a Timeline
```csharp
// Get events for a person
var events = await eventService
  .GetTimeline(userId, personId, startDate, endDate);

// Sort chronologically
events.OrderBy(e => e.EventDate);
```

### 🐛 Debugging Tips

#### Enable Verbose Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Coflnet.Connections": "Trace"
    }
  }
}
```

#### Check Database
```bash
# Connect to Cassandra
cqlsh localhost

# List tables
USE connections;
DESCRIBE TABLES;

# Query data
SELECT * FROM person LIMIT 10;
```

#### Common Issues

**Issue**: Tables not created  
**Fix**: Run the API once - tables auto-create

**Issue**: Relationship types missing  
**Fix**: Tables initialize automatically on first run

**Issue**: Search returns nothing  
**Fix**: Ensure search entries are added when creating entities

### 📊 Performance Notes

- **Client-side filtering**: Some queries filter in memory (see service logs)
- **Pagination**: Not yet implemented - coming in Phase 6
- **Caching**: Not yet implemented - coming in Phase 6
- **Scale**: Designed for thousands of users, thousands of entities each

### 🔐 Security

- JWT authentication required on all endpoints
- User ID extracted from JWT claims
- Privacy levels control data visibility
- CORS currently allows all (change for production)

### 🌍 Multi-Language

Relationship types support German and English:
```csharp
// German
"Mutter", "Vater", "Kind", "Bruder", "Schwester"

// English
"Mother", "Father", "Child", "Brother", "Sister"

// Add custom types
await relationshipService.AddRelationshipType(
  "Patenonkel", "de", "Patenonkel", "Patenkind", "family"
);
```

### 📝 Adding a New Feature

#### Example: Add "Photo" Entity

1. **Create Model** (`Models/Photo.cs`)
```csharp
public class Photo : BaseEntity {
  public string Url { get; set; }
  public Guid LinkedEntityId { get; set; }
  // ...
}
```

2. **Create Service** (`Services/PhotoService.cs`)
```csharp
public class PhotoService {
  // CRUD operations
}
```

3. **Create Controller** (`Controllers/PhotoController.cs`)
```csharp
[ApiController]
[Route("api/[controller]")]
public class PhotoController : ControllerBase {
  // Endpoints
}
```

4. **Register Service** (`Program.cs`)
```csharp
builder.Services.AddSingleton<PhotoService>();
```

5. **Add Tests** (`ConApi.Tests/Models/PhotoTests.cs`)
```csharp
public class PhotoTests {
  [Fact]
  public void Photo_ShouldInitialize() { ... }
}
```

### 🧪 Testing Strategy

```
Unit Tests (ConApi.Tests/)
├── Models/       # Entity initialization, properties
├── Services/     # Business logic (with mocks)
└── Controllers/  # HTTP endpoints (with WebApplicationFactory)

Integration Tests (Future)
└── Use TestContainers for Cassandra
```

### 📚 Resources

- **Cassandra Docs**: https://cassandra.apache.org/doc/
- **ASP.NET Core**: https://docs.microsoft.com/aspnet/core
- **xUnit**: https://xunit.net/
- **FluentAssertions**: https://fluentassertions.com/

### ❓ FAQ

**Q: Where are the relationship types stored?**  
A: `relationship_type` table, auto-populated on startup

**Q: How do I add a new relationship type?**  
A: POST to `/api/relationship/types` or add in `RelationshipService.InitializeDefaultRelationshipTypes()`

**Q: Can I change the privacy level later?**  
A: Yes, update the entity with new `PrivacyLevel`

**Q: How do I delete a relationship?**  
A: DELETE `/api/relationship/{id}` - automatically deletes both directions

**Q: Why are some queries slow?**  
A: Client-side filtering. Add materialized views for production.

**Q: How do I add a custom field to Person?**  
A: Use `PersonData` table - no schema change needed

### 🎯 Next Steps

1. Read the full **README.md**
2. Try the API with Swagger
3. Run the tests
4. Check **MIGRATION.md** for frontend integration
5. Review **PLAN.txt** for upcoming features

### 💡 Pro Tips

1. **Use DTOs** - Don't expose database models directly
2. **Check logs** - Services log warnings for performance issues
3. **Test with real data** - Use mock data generator
4. **Privacy matters** - Always set appropriate privacy levels
5. **Bidirectional** - One relationship call creates both directions

---

**Need Help?**
- Check the comprehensive **README.md**
- Review **IMPLEMENTATION_SUMMARY.md** for what's built
- See **MIGRATION.md** for frontend examples
- All tests pass - use them as examples!

**Happy Coding!** 🚀
