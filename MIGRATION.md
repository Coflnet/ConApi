# API Migration Guide - v1 to v2

## 🔄 Breaking Changes

### ⚠️ Search Endpoint
**FIXED TYPO** - Update your frontend code!

```diff
- GET /api/search?value=Fritz  (Method name was "Serach")
+ GET /api/search?value=Fritz  (Method name is now "Search")
```

### ✨ Enhanced Search Response
The search response now includes an entity type:

```diff
{
  "name": "Fritz Schmidt",
  "description": "fritz",
  "id": "guid-here",
- "link": null
+ "link": null,
+ "type": "Person"
}
```

**New `type` values**: `"Person"`, `"Location"`, `"Unknown"`

## 🆕 New Endpoints

### Places API
```
GET    /api/place/{id}
GET    /api/place/{id}/children
POST   /api/place
POST   /api/place/{id}/data
GET    /api/place/{id}/data?category={category}
```

### Things API
```
GET    /api/thing/{id}
GET    /api/thing/owner/{ownerId}
POST   /api/thing
POST   /api/thing/{id}/data
GET    /api/thing/{id}/data?category={category}
```

### Events API
```
GET    /api/event/{id}
POST   /api/event/timeline
GET    /api/event/type/{type}
GET    /api/event/range?startDate={date}&endDate={date}
POST   /api/event
POST   /api/event/{id}/data
GET    /api/event/{id}/data
```

### Relationships API
```
GET    /api/relationship/entity/{entityId}?primaryOnly={bool}
GET    /api/relationship/between?fromId={id}&toId={id}&type={type}
GET    /api/relationship/entity/{id}/type/{type}
GET    /api/relationship/path?startEntityId={id}&endEntityId={id}&maxDepth={n}
POST   /api/relationship
PUT    /api/relationship/{id}
DELETE /api/relationship/{id}
GET    /api/relationship/types?language={lang}
POST   /api/relationship/types
```

## 📊 New Data Models

### Place
```typescript
interface Place {
  id: string;
  userId: string;
  name: string;
  type: PlaceType;
  parentPlaceId?: string;
  latitude?: number;
  longitude?: number;
  hierarchyPath?: string;
  privacyLevel: PrivacyLevel;
  createdAt: string;
  updatedAt: string;
}

enum PlaceType {
  Unknown = 0,
  Continent = 1,
  Country = 2,
  State = 3,
  County = 4,
  City = 5,
  Village = 6,
  District = 7,
  Building = 8,
  Other = 99
}
```

### Thing
```typescript
interface Thing {
  id: string;
  userId: string;
  name: string;
  type: ThingType;
  ownerId?: string;
  manufacturer?: string;
  yearMade?: number;
  model?: string;
  serialNumber?: string;
  privacyLevel: PrivacyLevel;
  createdAt: string;
  updatedAt: string;
}

enum ThingType {
  Unknown = 0,
  Vehicle = 1,
  Building = 2,
  Toy = 3,
  Tool = 4,
  Furniture = 5,
  Jewelry = 6,
  Document = 7,
  Book = 8,
  Artwork = 9,
  Clothing = 10,
  Other = 99
}
```

### Event
```typescript
interface Event {
  id: string;
  userId: string;
  type: EventType;
  title: string;
  eventDate: string;
  endDate?: string;
  description?: string;
  targetEntityType: EntityType;
  targetEntityId: string;
  placeId?: string;
  privacyLevel: PrivacyLevel;
  createdAt: string;
  updatedAt: string;
}

enum EventType {
  Unknown = 0,
  Birth = 1,
  Death = 2,
  Marriage = 3,
  Divorce = 4,
  Move = 5,
  Education = 6,
  Employment = 7,
  Purchase = 8,
  Sale = 9,
  Accident = 10,
  Achievement = 11,
  Meeting = 12,
  Trip = 13,
  Memory = 14,
  Other = 99
}
```

### Relationship
```typescript
interface Relationship {
  id: string;
  userId: string;
  fromEntityType: EntityType;
  fromEntityId: string;
  toEntityType: EntityType;
  toEntityId: string;
  relationshipType: string;
  language: string;
  startDate?: string;
  endDate?: string;
  certainty: number;
  source?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
  isPrimary: boolean;
}

interface RelationshipType {
  type: string;
  language: string;
  displayName: string;
  inverseType?: string;
  category: string;
}

enum EntityType {
  Unknown = 0,
  Person = 1,
  Place = 2,
  Thing = 3,
  Event = 4
}
```

### Privacy Levels
```typescript
enum PrivacyLevel {
  Private = 0,   // Only owner can see
  Family = 1,    // Shared with family
  Friends = 2,   // Shared with friends
  Public = 3     // Everyone can see
}
```

## 🎯 Common Use Cases

### 1. Display Family Tree

```typescript
// Get person
const person = await fetch(`/api/person/${personId}`);

// Get all family relationships
const relationships = await fetch(`/api/relationship/entity/${personId}?primaryOnly=true`);

// Filter family relationships
const family = relationships.filter(r => 
  ['Mutter', 'Vater', 'Kind', 'Ehepartner', 'Bruder', 'Schwester'].includes(r.relationshipType)
);

// For each relationship, fetch related person
for (const rel of family) {
  const relatedPerson = await fetch(`/api/person/${rel.toEntityId}`);
  // Display in tree
}
```

### 2. Display Timeline

```typescript
// Get all events for a person
const response = await fetch('/api/event/timeline', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    entityType: 1, // Person
    entityId: personId,
    startDate: '1980-01-01',
    endDate: '2025-12-31'
  })
});

const events = await response.json();

// Sort chronologically
events.sort((a, b) => new Date(a.eventDate) - new Date(b.eventDate));

// Display timeline
events.forEach(event => {
  console.log(`${event.eventDate}: ${event.title}`);
});
```

### 3. Show Person's Belongings

```typescript
// Get all things owned by person
const things = await fetch(`/api/thing/owner/${personId}`);

// Get purchase events for each thing
for (const thing of things) {
  const purchaseEvents = await fetch('/api/event/timeline', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      entityType: 3, // Thing
      entityId: thing.id
    })
  });
}
```

### 4. Create Person with Family

```typescript
// 1. Create person
const person = await fetch('/api/person', {
  method: 'POST',
  body: JSON.stringify({
    name: 'Schmidt',
    birthday: '1980-05-15',
    birthPlace: 'München',
    displayName: 'Hans Schmidt',
    gender: 1,
    privacyLevel: 1
  })
});

// 2. Create birth event
await fetch('/api/event', {
  method: 'POST',
  body: JSON.stringify({
    type: 1, // Birth
    title: 'Geburt von Hans',
    eventDate: '1980-05-15',
    targetEntityType: 1,
    targetEntityId: person.id,
    privacyLevel: 1
  })
});

// 3. Link to mother
await fetch('/api/relationship', {
  method: 'POST',
  body: JSON.stringify({
    fromEntityType: 1,
    fromEntityId: motherId,
    toEntityType: 1,
    toEntityId: person.id,
    relationshipType: 'Mutter',
    language: 'de',
    certainty: 100
  })
});
```

### 5. Build Place Hierarchy

```typescript
// Create country
const country = await fetch('/api/place', {
  method: 'POST',
  body: JSON.stringify({
    name: 'Deutschland',
    type: 2 // Country
  })
});

// Create state
const state = await fetch('/api/place', {
  method: 'POST',
  body: JSON.stringify({
    name: 'Bayern',
    type: 3, // State
    parentPlaceId: country.id
  })
});

// Get all states in country
const states = await fetch(`/api/place/${country.id}/children`);
```

### 6. Search with Type Filtering

```typescript
// Search for people named Fritz
const results = await fetch('/api/search?value=Fritz');

// Filter by type
const people = results.filter(r => r.type === 'Person');
const places = results.filter(r => r.type === 'Location');
```

### 7. Find Relationship Path

```typescript
// How is Maria related to Hans?
const path = await fetch(
  `/api/relationship/path?startEntityId=${hansId}&endEntityId=${mariaId}&maxDepth=3`
);

// Build description
const description = path.map(r => r.relationshipType).join(' → ');
// Example: "Kind → Schwester" = Hans's sibling's child = Hans's niece
```

## 🌍 Multi-Language Support

### Get Relationship Types by Language

```typescript
// German
const deTypes = await fetch('/api/relationship/types?language=de');
// Returns: Mutter, Vater, Kind, etc.

// English
const enTypes = await fetch('/api/relationship/types?language=en');
// Returns: Mother, Father, Child, etc.
```

### Create Custom Relationship Type

```typescript
await fetch('/api/relationship/types', {
  method: 'POST',
  body: JSON.stringify({
    type: 'Patenonkel',
    language: 'de',
    displayName: 'Patenonkel',
    inverseType: 'Patenkind',
    category: 'family'
  })
});
```

## 🔐 Privacy Level Usage

```typescript
// Set privacy when creating
await fetch('/api/person', {
  method: 'POST',
  body: JSON.stringify({
    name: 'Schmidt',
    privacyLevel: 0 // Private - only you can see
  })
});

// Later change to family-visible
await fetch('/api/person', {
  method: 'POST',
  body: JSON.stringify({
    id: existingId,
    privacyLevel: 1 // Family - shared with family
  })
});
```

## 📱 UI Recommendations

### 1. Family Tree View
- Use relationship path finding for "Show how X and Y are related"
- Filter relationships by type for clean display
- Show bidirectional relationships only once (use `primaryOnly=true`)

### 2. Timeline View
- Sort events chronologically
- Group by year/decade
- Show event types with icons
- Link to places where events occurred

### 3. Person Detail View
- Display flexible attributes by category
- Show all relationships in tabs (Family, Ownership, etc.)
- Include timeline of life events
- List owned things

### 4. Place Hierarchy
- Display as breadcrumb navigation
- Show children in dropdown/tree
- Display on map using lat/long

### 5. Search
- Implement debounced search
- Show entity type icons
- Filter by entity type
- Highlight exact matches first

## ⚙️ Frontend Configuration

### API Base URL
```typescript
const API_BASE = process.env.REACT_APP_API_URL || 'https://con.coflnet.com/api';
```

### Authentication Header
```typescript
const headers = {
  'Authorization': `Bearer ${jwt_token}`,
  'Content-Type': 'application/json',
  'Accept-Language': 'de' // or 'en'
};
```

## 🐛 Common Issues

### Issue: Person ID format changed
**Old**: `"Name;1980-05-15;München"`  
**New**: GUID format `"550e8400-e29b-41d4-a716-446655440000"`

**Solution**: Update ID parsing logic. GUIDs are now returned directly.

### Issue: Search endpoint returns 404
**Cause**: Typo fix - method renamed from `Serach` to `Search`  
**Solution**: Update frontend route

### Issue: Missing relationship types
**Cause**: Database not initialized  
**Solution**: Relationship types auto-initialize on first API start

## 📊 Performance Tips

1. **Use `primaryOnly=true`** when querying relationships to avoid duplicates
2. **Implement caching** for relationship types (they rarely change)
3. **Batch requests** when building family trees
4. **Use date filters** on timeline queries to reduce data
5. **Implement pagination** for large result sets (coming in Phase 6)

## 🔄 Backward Compatibility

### Person API
The existing Person endpoints remain functional:
- `GET /api/person/{id}` still works
- `POST /api/person` still works with existing schema

### Search API
The Search endpoint is backward compatible except for the typo fix.

## 📝 Next Frontend Updates Needed

1. **Update search calls** to use correct method name
2. **Add entity type filtering** to search UI
3. **Implement relationship visualization** (family tree)
4. **Add timeline view** for persons/places/things
5. **Create place hierarchy browser**
6. **Add thing management** UI
7. **Implement event creation** forms
8. **Add multi-language** relationship type selector

---

**Questions?** Check the [README.md](README.md) for full API documentation or the Swagger docs at `https://con.coflnet.com/api/swagger`
