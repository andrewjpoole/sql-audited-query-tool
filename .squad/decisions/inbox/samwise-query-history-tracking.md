# Query History Tracking for AI-Initiated Queries

**Date:** 2026-02-22  
**Author:** Samwise (Backend Dev)  
**Status:** IMPLEMENTED  
**Topic:** Ensure AI-initiated queries (via Ollama tool calling) flow through history/audit pipeline

---

## Context

Gandalf's architecture decision recommended **app-orchestrated tool calling** where Ollama can request query execution through our existing QueryExecutor pipeline. This decision ensures that when Ollama executes queries via tool calling, they:

1. Create QueryHistory entries (same as user queries)
2. Flow through the audit service (GitHub issue posting)
3. Are marked as AI-initiated (not user-initiated)
4. Return QueryHistory ID for frontend display
5. Maintain readonly enforcement

---

## Implementation

### New Models

**`QueryHistory` (Core/Models/QueryHistory.cs)**
- Added comprehensive history tracking model with:
  - `Id` (Guid) — unique identifier for each query execution
  - `Source` (enum: User | AI) — tracks query origin
  - `RequestedBy` (string) — "Ollama" for AI queries, user identity for user queries
  - Full execution metadata (SQL, timestamp, row count, execution time, success status)
  - `GitHubIssueUrl` — link to audit trail

**`QuerySource` enum**
- `User` — queries initiated through UI by humans
- `AI` — queries initiated by Ollama via tool calling

### New Infrastructure

**`IQueryHistoryStore` (Core/Interfaces/IQueryHistoryStore.cs)**
- Interface for query history persistence
- Methods:
  - `AddAsync` — store new history entry
  - `GetAllAsync(limit)` — retrieve recent history (default 100)
  - `GetByIdAsync(id)` — retrieve specific entry

**`InMemoryQueryHistoryStore` (Database/InMemoryQueryHistoryStore.cs)**
- Thread-safe in-memory implementation using `ConcurrentDictionary` + `ConcurrentQueue`
- Registered as singleton in DI
- Maintains insertion order for chronological retrieval
- Future: Can be replaced with DB-backed implementation without changing consumers

### API Changes

**`POST /api/query/execute` (Program.cs)**
- Added `IQueryHistoryStore` dependency injection
- Added optional `Source` field to `ExecuteQueryRequest` DTO
  - `"AI"` for Ollama-initiated queries
  - `null` or other value for user queries
- Sets `RequestedBy = "Ollama"` when `Source == "AI"`
- Creates `QueryHistory` entry after successful audit
- Returns `historyId` in response for frontend tracking

**`GET /api/query/history` (Program.cs)**
- Replaced placeholder with real implementation
- Returns recent query history with configurable limit
- Response includes all metadata: SQL, source, timestamp, results, audit URL

---

## Integration Points for Radagast

When implementing Ollama tool calling:

1. **Tool call request:**
   ```json
   POST /api/query/execute
   {
     "sql": "SELECT * FROM Users WHERE Active = 1",
     "source": "AI"
   }
   ```

2. **Response includes:**
   ```json
   {
     "historyId": "guid-here",
     "columns": [...],
     "rows": [...],
     "rowCount": 42,
     "executionTimeMs": 123,
     "auditUrl": "https://github.com/..."
   }
   ```

3. **History retrieval:**
   ```
   GET /api/query/history?limit=50
   ```

---

## Security Guarantees

✅ **Readonly enforcement** — All queries (user + AI) pass through `SqlQueryExecutor.ValidateQuery()` which blocks INSERT/UPDATE/DELETE/etc.  
✅ **Audit trail** — All queries flow through `GitHubAuditLogger` before history storage  
✅ **Source tracking** — QueryHistory clearly marks AI vs User origin  
✅ **No bypass path** — Tool calling uses same `/api/query/execute` endpoint as UI  

---

## Testing Notes

- Build verification: ✅ Core and Database projects compile cleanly
- App build blocked by running process (expected — hot reload active)
- Manual testing required once app restarts to verify:
  - History entries created for both user and AI queries
  - Source field correctly differentiates origins
  - History endpoint returns data chronologically

---

## Future Enhancements

1. **Persistent storage** — Replace `InMemoryQueryHistoryStore` with EF Core/SQL backed store
2. **Filtering** — Add query params to `/api/query/history` (by source, date range, success status)
3. **Pagination** — Cursor-based pagination for large history sets
4. **Retention policy** — Auto-cleanup of old history entries

---

**Status:** Ready for Radagast to implement tool calling integration on the LLM side.
