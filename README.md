# Connections API - Implementation Summary

## 🎯 Overview

The Connections API has been comprehensively refactored and extended to support a full genealogy and personal history tracking system. The API now supports:

- **Persons** - Track individuals with flexible attributes
- **Places** - Hierarchical location management
- **Things** - Physical objects (cars, toys, documents, etc.)
- **Events** - Timeline and memory functionality
- **Relationships** - Bidirectional connections between any entities
- **Search** - Fast, fuzzy search across all entities
- **Multi-language** - German (primary) and English support

## 📊 Current Status

### ✅ Completed (Phase 1 & 2)

1. **Critical Bug Fixes**
   - Fixed typo: `Serach` → `Search`
   - Removed duplicate query in SearchService
   - Added global exception handling middleware
   - Added Type field to SearchResult

2. **Core Infrastructure**
   - Created base entity classes with privacy levels
   - Added DTOs for all entities
   - Implemented API response wrappers
   - Set up comprehensive test project (11/11 tests passing)

3. **New Entity Models**
   - `Person` - With unique ID, gender, birth/death tracking
   - `Place` - With hierarchical support and geo-coordinates
   - `Thing` - For physical objects with ownership tracking
   - `Event` - For timeline functionality
   - `Relationship` - Bidirectional with metadata

4. **Services Implemented**
   - `PlaceService` - Place management with hierarchy
   - `ThingService` - Thing management with owner tracking
   - `EventService` - Event and timeline management
   - `RelationshipService` - Bidirectional relationship management with path finding

5. **Controllers Created**
   - `PlaceController` - Full CRUD for places
   - `ThingController` - Full CRUD for things
   - `EventController` - Event management and timeline queries
   - `RelationshipController` - Relationship CRUD and path finding

## 🏗️ Architecture

### Entity Structure

All entities follow a consistent pattern:

```
Core Entity (fixed schema)
├── Id (GUID) - Unique identifier
├── UserId (GUID) - Owner
├── PrivacyLevel - Access control
└── Core fields (name, dates, etc.)

EntityData (flexible attributes)
├── UserId + EntityId (partition key)
├── Category/Key (clustering keys)
└── Value (flexible content)
```

### Key Features

#### 1. **Hierarchical Places**
```csharp
Place
├── ParentPlaceId → Supports tree structure
├── HierarchyPath → Full path for display
└── Lat/Long → Geo-coordinates
```

#### 2. **Bidirectional Relationships**
```csharp
Relationship
├── From/To entities
├── Type (e.g., "Mutter", "Vater", "Besitzer")
├── Language (de/en)
├── Metadata (dates, certainty, source)
└── IsPrimary → Tracks direction
```

**Pre-defined German relationships:**
- Family: Mutter, Vater, Kind, Bruder, Schwester, Ehepartner, Großmutter, Großvater, Onkel, Tante
- Ownership: Besitzer, Besitz

#### 3. **Timeline/Events**
```csharp
Event
├── TargetEntity → Links to Person/Place/Thing
├── EventDate/EndDate → Time span support
├── Type → Birth, Death, Marriage, Purchase, Memory, etc.
└── PlaceId → Where it happened
```

#### 4. **Search System**
- Multi-word fuzzy search using Levenshtein distance
- Entity type filtering
- Exact match prioritization
- Searchable: Persons, Places, Things, Events

## 📡 API Endpoints

### Places
```
GET    /api/place/{id}              - Get place by ID
GET    /api/place/{id}/children     - Get child places
POST   /api/place                   - Create/update place
POST   /api/place/{id}/data         - Add flexible attribute
GET    /api/place/{id}/data         - Get attributes
```

### Things
```
GET    /api/thing/{id}              - Get thing by ID
GET    /api/thing/owner/{ownerId}   - Get things by owner
POST   /api/thing                   - Create/update thing
POST   /api/thing/{id}/data         - Add flexible attribute
GET    /api/thing/{id}/data         - Get attributes
```

### Events
```
GET    /api/event/{id}              - Get event by ID
POST   /api/event/timeline          - Get timeline for entity
GET    /api/event/type/{type}       - Get events by type
GET    /api/event/range             - Get events in date range
POST   /api/event                   - Create/update event
POST   /api/event/{id}/data         - Add flexible attribute
GET    /api/event/{id}/data         - Get attributes
```

### Relationships
```
GET    /api/relationship/entity/{entityId}              - Get all relationships
GET    /api/relationship/between                        - Get specific relationship
GET    /api/relationship/entity/{id}/type/{type}        - Get by type
GET    /api/relationship/path                           - Find path between entities
POST   /api/relationship                                - Create relationship
PUT    /api/relationship/{id}                           - Update relationship
DELETE /api/relationship/{id}                           - Delete relationship
GET    /api/relationship/types                          - Get relationship types
POST   /api/relationship/types                          - Add custom type
```

### Search (Existing, Updated)
```
GET    /api/search?value={query}    - Search all entities
POST   /api/search                  - Add search entry
```

### Person (Existing)
```
GET    /api/person/{id}             - Get person data
POST   /api/person                  - Add person data
```

## 🗄️ Database Schema (Cassandra)

### Tables Created
1. `person` - Core person data
2. `person_data` - Flexible person attributes
3. `place` - Core place data
4. `place_data` - Flexible place attributes
5. `thing` - Core thing data
6. `thing_data` - Flexible thing attributes
7. `event` - Core event data
8. `event_data` - Flexible event attributes
9. `relationship` - Bidirectional relationships
10. `relationship_type` - Relationship translations
11. `search_entry` - Search index

### Indexing Strategy
- Primary keys optimized for user-scoped queries
- Partition keys include `UserId` for data isolation
- Clustering keys for sorting and uniqueness
- **Note**: Some queries use client-side filtering (marked in logs)
  - Consider materialized views for production at scale

## 🔒 Security & Privacy

### Privacy Levels
```csharp
enum PrivacyLevel
{
    Private = 0,   // Only owner
    Family = 1,    // Shared with family
    Friends = 2,   // Shared with friends
    Public = 3     // Publicly visible
}
```

### Authentication
- JWT-based authentication (Coflnet.Auth)
- All endpoints require `[Authorize]` attribute
- User ID extracted from JWT claims

### CORS
- Currently allows all origins (configured for development)
- Should be environment-specific in production

## 🧪 Testing

### Test Coverage
```
✓ PersonTests (4 tests)
✓ PlaceTests (2 tests)
✓ RelationshipTests (3 tests)
✓ EventTests (3 tests)

Total: 11/11 tests passing
```

### Test Project
- xUnit with FluentAssertions
- Moq for mocking
- Ready for integration tests with TestContainers

## 🚀 Usage Examples

### Example 1: Create a Person with Family Tree

```csharp
// 1. Create person
POST /api/person
{
  "name": "Schmidt",
  "birthday": "1980-05-15",
  "birthPlace": "München",
  "displayName": "Hans Schmidt",
  "gender": 1,
  "privacyLevel": 1
}

// 2. Add flexible attributes
POST /api/person/{personId}/data
{
  "category": "contact",
  "key": "email",
  "value": "hans@example.com"
}

// 3. Create parent relationship
POST /api/relationship
{
  "fromEntityType": 1,
  "fromEntityId": "{motherId}",
  "toEntityType": 1,
  "toEntityId": "{personId}",
  "relationshipType": "Mutter",
  "language": "de",
  "certainty": 100
}
```

### Example 2: Track a Car

```csharp
// 1. Create thing (car)
POST /api/thing
{
  "name": "VW Golf",
  "type": 1,
  "manufacturer": "Volkswagen",
  "yearMade": 2010,
  "model": "Golf VII",
  "ownerId": "{personId}",
  "privacyLevel": 0
}

// 2. Create ownership relationship
POST /api/relationship
{
  "fromEntityType": 1,
  "fromEntityId": "{personId}",
  "toEntityType": 3,
  "toEntityId": "{carId}",
  "relationshipType": "Besitzer",
  "language": "de",
  "startDate": "2010-05-01"
}

// 3. Add purchase event
POST /api/event
{
  "type": 8,
  "title": "Erstes Auto gekauft",
  "eventDate": "2010-05-01",
  "targetEntityType": 3,
  "targetEntityId": "{carId}",
  "description": "VW Golf, blau"
}
```

### Example 3: Build Place Hierarchy

```csharp
// 1. Create country
POST /api/place
{
  "name": "Deutschland",
  "type": 2
}

// 2. Create state
POST /api/place
{
  "name": "Bayern",
  "type": 3,
  "parentPlaceId": "{countryId}"
}

// 3. Create city
POST /api/place
{
  "name": "München",
  "type": 5,
  "parentPlaceId": "{stateId}",
  "latitude": 48.1351,
  "longitude": 11.5820
}

// Query children
GET /api/place/{countryId}/children
```

### Example 4: Timeline Query

```csharp
// Get all events for a person
POST /api/event/timeline
{
  "entityType": 1,
  "entityId": "{personId}",
  "startDate": "1980-01-01",
  "endDate": "2025-12-31"
}

// Response includes births, marriages, purchases, memories, etc.
```

### Example 5: Find Relationship Path

```csharp
// Find how two people are related
GET /api/relationship/path?startEntityId={johnId}&endEntityId={mariaId}&maxDepth=3

// Response: [
//   { "fromId": "john", "toId": "mother", "type": "Kind" },
//   { "fromId": "mother", "toId": "maria", "type": "Schwester" }
// ]
// Result: John's mother's sister = John's aunt
```

## 📋 Next Steps (Phase 3-8)

### Phase 3: Data Sharing
- [ ] Implement sharing invitations
- [ ] Add conflict resolution
- [ ] Add merge/keep-both strategies
- [ ] Track data provenance
- [ ] Full dataset export/import as single file

### Phase 4: Document Management
- [ ] S3 integration for file storage (cloudflare r2)
- [ ] Document linking to entities
- [ ] Storage quota tracking
- [ ] Presigned URL generation

### Phase 5: Enhanced Search
- [ ] Multi-word search improvements
- [ ] Date range filtering
- [ ] Relational search ("John's Uncle")
- [ ] Pagination support
- [ ] Consider OpenSearch migration

### Phase 6: Production Readiness
- [ ] Add materialized views for filtered queries
- [ ] Implement caching layer
- [ ] Add rate limiting
- [ ] Add health checks
- [ ] Improve logging and monitoring
- [ ] Add API versioning

### Phase 7: Advanced Features
- [ ] Source citation tracking
- [ ] Conflicting information handling
- [ ] Photo/document display
- [ ] Export functionality (GEDCOM, JSON)
- [ ] Family tree visualization data

## 🛠️ Development

### Build
```bash
cd ConApi
dotnet build
```

### Run Tests
```bash
cd ConApi.Tests
dotnet test
```

### Run Locally
```bash
cd ConApi
dotnet run
```

Access Swagger: `https://localhost:5001/swagger`

### Docker Build
```bash
docker build -t connections-api .
```

## 📝 Notes

- All services use Cassandra for persistence
- Swagger documentation auto-generated via Coflnet.Core
- Firebase authentication configured (optional)
- Designed for Kubernetes deployment
- Multi-language support built-in (German/English)
- Flexible attribute system allows future extensibility
- Privacy-first design with granular access control

## ⚠️ Known Limitations

1. **Client-side Filtering**: Some queries (children, owners, timelines) currently use client-side filtering. Production deployment should add materialized views.

2. **Delete Operations**: Cassandra delete operations need careful handling of composite keys.

3. **Search Scalability**: Current search uses in-memory Levenshtein. Consider OpenSearch for larger deployments.

4. **No Caching**: Implement Redis caching for frequently accessed data.

5. **No Pagination**: Add pagination to list endpoints for better performance.

## 📚 References

- [Cassandra Data Modeling](https://cassandra.apache.org/doc/latest/data_modeling/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [GEDCOM Standard](https://www.gedcom.org/) - For future export compatibility

---

**Last Updated**: 2025-10-24  
**Version**: 2.0.0-alpha  
**Status**: Phase 1 & 2 Complete, Ready for Testing
