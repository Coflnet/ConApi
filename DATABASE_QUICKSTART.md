# Database Initialization Quick Reference

## Quick Commands

### Check Status
```bash
curl http://localhost:5000/api/admin/initialization-status
```

### Initialize Database (with auth token)
```bash
# Get your token from .test_token file
TOKEN=$(cat .test_token)

# Initialize database
curl -X POST "http://localhost:5000/api/admin/initialize-database?force=true" \
  -H "Authorization: Bearer $TOKEN"
```

### Test Database
```bash
TOKEN=$(cat .test_token)

curl -X POST "http://localhost:5000/api/admin/test-database" \
  -H "Authorization: Bearer $TOKEN"
```

### Full Reset (Drop → Create → Test)
```bash
TOKEN=$(cat .test_token)

curl -X POST "http://localhost:5000/api/admin/full-initialization-cycle" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" | jq
```

## Development Workflow

### 1. Start with Clean Database
```bash
TOKEN=$(cat .test_token)

# Drop all tables
curl -X POST "http://localhost:5000/api/admin/drop-all-tables" \
  -H "Authorization: Bearer $TOKEN"
```

### 2. Make Schema Changes
Edit the `EnsureSchema()` method in the relevant service file:
- `Services/ThingService.cs`
- `Services/PlaceService.cs`
- `Services/EventService.cs`
- etc.

### 3. Recreate Tables
```bash
# Restart the app (tables auto-create on startup)
# OR use the endpoint:
curl -X POST "http://localhost:5000/api/admin/initialize-database?force=true" \
  -H "Authorization: Bearer $TOKEN"
```

### 4. Verify
```bash
curl -X POST "http://localhost:5000/api/admin/test-database" \
  -H "Authorization: Bearer $TOKEN" | jq
```

## All-in-One Script
Save as `reset-db.sh`:

```bash
#!/bin/bash
TOKEN=$(cat .test_token 2>/dev/null)

if [ -z "$TOKEN" ]; then
    echo "Error: .test_token file not found"
    exit 1
fi

echo "🔄 Running full initialization cycle..."
curl -X POST "http://localhost:5000/api/admin/full-initialization-cycle" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -s | jq

echo ""
echo "✅ Done!"
```

Make executable: `chmod +x reset-db.sh`
Run: `./reset-db.sh`

## Troubleshooting

### Table Not Found Errors
If you get "unconfigured table" errors:
1. Check if database was initialized: `GET /api/admin/initialization-status`
2. Manually initialize: `POST /api/admin/initialize-database?force=true`
3. Check logs for initialization errors

### Authorization Errors
- Make sure `.test_token` file exists in project root
- Token is created automatically on application startup
- For production, use proper JWT tokens

### Schema Changes Not Reflecting
1. Drop all tables: `POST /api/admin/drop-all-tables`
2. Restart application (or force initialize)
3. Verify with test endpoint

## File Locations

### Service Files (where EnsureSchema is defined)
- `Services/ThingService.cs` - thing, thing_data, thing_by_owner
- `Services/PlaceService.cs` - place, place_data
- `Services/EventService.cs` - event, event_data
- `Services/RelationshipService.cs` - relationship, relationship_by_from, relationship_by_to, relationship_type
- `Services/ShareService.cs` - share_invitation, share_invitation_by_recipient, data_provenance, data_conflict
- `Services/DocumentService.cs` - document, document_link, document_by_entity, storage_quota
- `Services/SourceCitationService.cs` - source_citation, citation_by_source, conflicting_information
- `Persons/PersonService.cs` - person2
- `Search/SearchService.cs` - search_entry

### Configuration Files
- `Program.cs` - Startup initialization
- `Services/DatabaseInitializationService.cs` - Core initialization logic
- `Controllers/AdminController.cs` - HTTP endpoints
- `Services/GlobalMapping.cs` - Table mappings

## Table List (26 total)

1. person2
2. person_data
3. place
4. place_data
5. thing
6. thing_data
7. thing_by_owner
8. event
9. event_data
10. relationship
11. relationship_by_from
12. relationship_by_to
13. relationship_type
14. search_entry
15. share_invitation
16. share_invitation_by_recipient
17. data_provenance
18. data_conflict
19. document
20. document_link
21. document_by_entity
22. storage_quota
23. source_citation
24. citation_by_source
25. conflicting_information
26. migrations (created by MigrationRunner)

## Expected Response Examples

### Success
```json
{
  "success": true,
  "message": "Full initialization cycle completed successfully",
  "details": {
    "dropped_tables": ["person2", "place", "thing", ...],
    "drop_failures": [],
    "initialization_message": "Database initialized successfully",
    "test_results": {
      "person2": "OK - Table exists",
      "place": "OK - Table exists",
      ...
    }
  }
}
```

### Failure
```json
{
  "success": false,
  "message": "Failed to initialize database",
  "details": {
    "error": "Keyspace not configured"
  }
}
```
