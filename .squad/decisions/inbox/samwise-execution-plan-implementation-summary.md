# Execution Plan Feature — Backend Implementation Summary

**Date:** 2026-02-23  
**By:** Samwise (Backend Specialist)  
**Status:** ✅ COMPLETED  
**Related:** gandalf-execution-plan-feature.md

---

## Implementation Summary

Successfully implemented the complete backend portion of the execution plan feature as specified in the architecture document. All components are now ready for frontend integration by Legolas.

### Changes Implemented

#### 1. ✅ Model Updates

**QueryRequest.cs** — Added execution plan flag:
```csharp
public bool IncludeExecutionPlan { get; init; } = false;
```

**QueryResult.cs** — Added execution plan fields:
```csharp
public string? ExecutionPlanXml { get; init; }
public bool HasExecutionPlan => ExecutionPlanXml is not null;
```

**QueryHistory.cs** — Added audit flag (metadata only, NOT the XML):
```csharp
public bool IncludedExecutionPlan { get; init; }
```

#### 2. ✅ SqlQueryExecutor Implementation

**Location:** `src/SqlAuditedQueryTool.Database/SqlQueryExecutor.cs`

**Key Changes:**
- SQL wrapping when `request.IncludeExecutionPlan == true`:
  ```sql
  SET STATISTICS XML ON;
  <user query>
  SET STATISTICS XML OFF;
  ```
- Execution plan detection logic:
  - Checks for result set with exactly 1 row, 1 column
  - Validates XML content starts with `<ShowPlanXML` (case-insensitive)
  - Extracts plan into `QueryResult.ExecutionPlanXml`
  - **Excludes** plan result set from normal `ResultSets` collection
- Added logging for plan capture: `HasPlan={HasPlan}`

**Behavior:**
- When flag is `false`: No wrapping, standard query execution
- When flag is `true`: SQL is wrapped, plan is captured from last result set
- Plan XML is stored in `QueryResult.ExecutionPlanXml`, never in `ResultSets`

#### 3. ✅ API Updates

**Location:** `src/SqlAuditedQueryTool.App/Program.cs`

**Request DTO Updated:**
```csharp
record ExecuteQueryRequest(string Sql, string? Source, bool? IncludeExecutionPlan);
```

**Endpoint Changes:**

**POST /api/query/execute:**
- Accepts `includeExecutionPlan` boolean in request body
- Returns `executionPlanXml` in response (null if not requested/unavailable)
- Sets `IncludedExecutionPlan` flag in history entry
- Updated logging to include `HasPlan={HasPlan}`

**GET /api/query/history:**
- Added `includedExecutionPlan` field to response
- Shows whether execution plan was requested (metadata only)

#### 4. ✅ Testing

**Location:** `tests/SqlAuditedQueryTool.Database.Tests/SqlQueryExecutorTests.cs`

**Tests Added:**
1. `ExecuteReadOnlyQueryAsync_WithIncludeExecutionPlanFalse_DoesNotWrapSql`
2. `ExecuteReadOnlyQueryAsync_WithIncludeExecutionPlanTrue_FlagIsSet`
3. `QueryResult_WithExecutionPlanXml_HasExecutionPlanReturnsTrue`
4. `QueryResult_WithoutExecutionPlanXml_HasExecutionPlanReturnsFalse`
5. `QueryHistory_WithIncludedExecutionPlanFlag_StoresFlag`
6. `QueryRequest_DefaultIncludeExecutionPlan_IsFalse`

**Test Results:** ✅ All 85 tests passing (6 new + 79 existing)

---

## API Contract

### Request
```json
POST /api/query/execute
{
  "sql": "SELECT * FROM Users",
  "source": "User",
  "includeExecutionPlan": true
}
```

### Response
```json
{
  "historyId": "guid",
  "resultSets": [...],
  "executionTimeMs": 150,
  "auditUrl": "https://github.com/...",
  "executionPlanXml": "<ShowPlanXML xmlns=\"...\">...</ShowPlanXML>",
  "columns": [...],
  "rows": [...],
  "rowCount": 42
}
```

**Notes:**
- `executionPlanXml` is `null` when `includeExecutionPlan` is `false` or omitted
- Plan XML is 50-200KB for complex queries (transmitted as string, not parsed JSON)
- Plan is excluded from `resultSets` array

---

## Security Assessment

✅ **No new security concerns:**
- `SET STATISTICS XML ON` is a session setting, not a data modification
- Readonly enforcement remains unchanged
- Plans contain schema metadata (table/index names, join strategies) — same exposure as `SchemaTreeView`
- Plans show estimated row counts, not actual data
- No additional SQL injection vectors (flag is boolean, XML is read-only result)

---

## Performance Impact

- **Overhead:** ~5-10% execution time increase when flag is enabled
- **Network:** Plan XML adds 50-200KB to response payload for complex queries
- **Mitigation:** Opt-in only (default: `false`)

---

## Ready for Frontend Integration

The backend is fully implemented and tested. Legolas can now proceed with:

1. Adding "Show Plan" checkbox to `TabbedSqlEditor.tsx`
2. Installing `html-query-plan` npm package
3. Creating `ExecutionPlanView.tsx` component
4. Adding "Execution Plan" tab to `QueryResults.tsx`
5. Passing `includeExecutionPlan` flag through `queryApi.ts`

**API Contract:** Stable and ready for consumption  
**Testing:** All tests passing  
**Build:** Clean (no warnings/errors)

---

## Files Modified

**Backend:**
- ✅ `src/SqlAuditedQueryTool.Core/Models/QueryRequest.cs`
- ✅ `src/SqlAuditedQueryTool.Core/Models/QueryResult.cs`
- ✅ `src/SqlAuditedQueryTool.Core/Models/QueryHistory.cs`
- ✅ `src/SqlAuditedQueryTool.Database/SqlQueryExecutor.cs`
- ✅ `src/SqlAuditedQueryTool.App/Program.cs`

**Tests:**
- ✅ `tests/SqlAuditedQueryTool.Database.Tests/SqlQueryExecutorTests.cs` (NEW)
- ✅ `tests/SqlAuditedQueryTool.Database.Tests/SqlAuditedQueryTool.Database.Tests.csproj` (added Moq)

---

## Next Steps

**For Legolas (Frontend):**
1. Install `html-query-plan` via npm
2. Add checkbox to toolbar
3. Create plan viewer component with visual/XML toggle
4. Wire up API call with `includeExecutionPlan` flag
5. Persist checkbox state to localStorage

**Estimated Frontend Effort:** 3-4 hours (per architecture doc)

---

## Verification Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run just database tests
dotnet test tests/SqlAuditedQueryTool.Database.Tests/SqlAuditedQueryTool.Database.Tests.csproj

# Results:
# ✅ Build succeeded (no errors/warnings)
# ✅ 85 tests passed (6 new execution plan tests)
```

---

## Implementation Notes

1. **Plan Detection Logic:** Robust detection using three criteria:
   - Exactly 1 row
   - Exactly 1 column
   - XML content starts with `<ShowPlanXML` (case-insensitive)

2. **No Plan XML in Audit:** As specified, only the boolean flag is stored in history/audit — NOT the plan XML itself (too large for GitHub issues)

3. **Backward Compatibility:** All changes are additive:
   - Default `IncludeExecutionPlan = false` preserves existing behavior
   - New fields are optional/nullable
   - API responses include legacy fields for compatibility

4. **Logging Enhanced:** Added plan capture logging for observability:
   - "Execution plan captured: {XmlLength} characters"
   - "HasPlan={HasPlan}" in execution summary

---

**Status:** ✅ Backend implementation complete and tested. Ready for frontend integration.
