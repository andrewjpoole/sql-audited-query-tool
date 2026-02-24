# Execution Plan Feature — Architecture Proposal

**Date:** 2026-02-23  
**By:** Gandalf (Lead)  
**Status:** PROPOSED  
**Requested by:** Andrew

---

## Summary

Add a checkbox next to the Execute (F7) and Run Selection (F8) buttons. When checked, queries capture the SQL Server **actual execution plan** and display it alongside results in a new tab within the results panel.

---

## Architecture Decisions

### 1. Plan Capture Strategy: `SET STATISTICS XML ON`

**Chosen:** `SET STATISTICS XML ON` / `OFF` wrapping the user query.

**Why not alternatives:**
| Option | Behavior | Verdict |
|--------|----------|---------|
| `SET SHOWPLAN_XML ON` | Estimated plan only — **does NOT execute** the query | ❌ User wants plan + results together |
| `SET STATISTICS XML ON` | Executes query AND appends actual plan as final result set | ✅ Plan + results in one roundtrip |
| `EXPLAIN` | Not SQL Server syntax | ❌ N/A |

**How it works:** SQL Server appends the XML execution plan as an additional result set (single row, single column of XML) after all data result sets. Our executor already loops through result sets via `reader.NextResultAsync()` — we detect the plan result set and extract it.

**Implementation (Samwise):**
```csharp
// In SqlQueryExecutor.ExecuteReadOnlyQueryAsync()
if (request.IncludeExecutionPlan)
{
    // Wrap: SET STATISTICS XML ON; <user query>; SET STATISTICS XML OFF;
    wrappedSql = $"SET STATISTICS XML ON;\n{request.Sql}\nSET STATISTICS XML OFF;";
}
```

The plan XML appears as the **last result set** — a single row with a single `Microsoft SQL Server 2005 XML Showplan` column. The executor detects this by checking if the result set has exactly one row and one column containing XML starting with `<ShowPlanXML`.

### 2. Model Changes (Samwise)

**QueryRequest** — add:
```csharp
public bool IncludeExecutionPlan { get; init; } = false;
```

**QueryResult** — add:
```csharp
public string? ExecutionPlanXml { get; init; }
public bool HasExecutionPlan => ExecutionPlanXml is not null;
```

**API contract** — `/api/query/execute` request gains `includeExecutionPlan: boolean`, response gains `executionPlanXml: string | null`.

### 3. UI Design (Legolas)

#### Checkbox Placement
Add a **single checkbox** in the `editor-toolbar` div, between the two execute buttons and the tab controls:

```
[▶ Execute] [▶ Run Selection] [☑ Show Plan]
```

- Label: "Show Plan" (short, fits toolbar)
- State stored in React state, persisted to `localStorage` (like other UI preferences)
- Applies to BOTH Execute (F7) and Run Selection (F8)

#### Plan Display — New Tab in Results Panel
QueryResults.tsx already renders multiple result sets. Add a dedicated **"Execution Plan"** tab when plan data is present:

```
[Result Set 1] [Result Set 2] [📊 Execution Plan]
```

The plan tab has two sub-views toggled by a button:
1. **Visual Plan** (default) — render using `html-query-plan` npm package (renders SQL Server XML showplans as interactive HTML diagrams with operator nodes, costs, row counts)
2. **Raw XML** — collapsible `<pre>` block with syntax highlighting, plus a "Copy XML" button

**Why `html-query-plan`:** It's a lightweight, zero-dependency library specifically built for SQL Server XML showplan rendering. Same format SSMS uses. Interactive: hover shows operator details.

#### Visual Plan Rendering
```tsx
// New component: ExecutionPlanView.tsx
import QP from 'html-query-plan';

function ExecutionPlanView({ planXml }: { planXml: string }) {
  const containerRef = useRef<HTMLDivElement>(null);
  
  useEffect(() => {
    if (containerRef.current && planXml) {
      QP.showPlan(containerRef.current, planXml);
    }
  }, [planXml]);
  
  return (
    <div>
      <div className="plan-toolbar">
        <button onClick={toggleView}>
          {showXml ? '📊 Visual' : '📄 XML'}
        </button>
        <button onClick={copyXml}>📋 Copy XML</button>
      </div>
      {showXml ? (
        <pre className="plan-xml">{planXml}</pre>
      ) : (
        <div ref={containerRef} className="plan-visual" />
      )}
    </div>
  );
}
```

### 4. Performance Considerations

| Concern | Mitigation |
|---------|------------|
| Plan XML can be 50-200KB for complex queries | Only capture when checkbox is on; don't store in query history |
| `SET STATISTICS XML` adds ~5-10% overhead | Opt-in only; checkbox defaults to OFF |
| Large plans slow down JSON serialization | Transmit plan XML as a single string field, not parsed JSON |
| Plan rendering in browser | `html-query-plan` is lightweight; render lazily only when plan tab is active |

### 5. Audit Trail Integration

**Decision: Do NOT post execution plans to GitHub issues.**

Rationale:
- Plans can be 50-200KB of XML — too large for issue comments
- Plans expose index names, join strategies, estimated row counts (schema-level info, not data)
- We already expose schema via SchemaMetadataProvider, so no new security boundary
- Instead: log a **boolean flag** `includedExecutionPlan: true` in the audit entry

**QueryHistory** — add `IncludedExecutionPlan: bool` field (metadata only, not the plan itself).

### 6. Security Assessment

| Aspect | Risk | Notes |
|--------|------|-------|
| Row data exposure | ✅ None | Plans show estimated row counts, not actual data |
| Schema exposure | 🟡 Low | Plans show table/index names — same as SchemaTreeView already exposes |
| Query structure | 🟡 Low | Plans reveal join strategies, scan types — operational, not sensitive |
| Readonly enforcement | ✅ Unchanged | `SET STATISTICS XML ON` is a session setting, not a data modification |

**Verdict:** No additional security controls needed. Execution plans are schema-level metadata.

---

## Implementation Plan

### Phase 1 — Backend (Samwise)
1. Add `IncludeExecutionPlan` to `QueryRequest`
2. In `SqlQueryExecutor`, wrap SQL with `SET STATISTICS XML ON/OFF` when flag is true
3. Detect plan result set (last result set, single row, XML content starting with `<ShowPlanXML`)
4. Extract plan XML into `QueryResult.ExecutionPlanXml`, exclude from `ResultSets` collection
5. Update `/api/query/execute` to accept/return execution plan fields
6. Add `IncludedExecutionPlan` boolean to audit/history entries
7. Unit test: verify plan extraction, verify flag-off means no wrapping

### Phase 2 — Frontend (Legolas)
1. Install `html-query-plan` npm package
2. Add "Show Plan" checkbox to `editor-toolbar` in `TabbedSqlEditor.tsx`
3. Pass `includeExecutionPlan` flag through to `queryApi.ts` fetch call
4. Create `ExecutionPlanView.tsx` component (visual + XML toggle)
5. Add "Execution Plan" tab to `QueryResults.tsx` when plan data present
6. Persist checkbox state to `localStorage`
7. Style plan view (scrollable container, operator node styling via `html-query-plan` CSS)

### Estimated Effort
- **Samwise (backend):** ~2-3 hours — straightforward SQL wrapping + model changes
- **Legolas (frontend):** ~3-4 hours — new component + plan rendering library integration

---

## Files Affected

**Backend:**
- `src/SqlAuditedQueryTool.Core/Models/QueryRequest.cs` — add flag
- `src/SqlAuditedQueryTool.Core/Models/QueryResult.cs` — add plan XML field
- `src/SqlAuditedQueryTool.Database/SqlQueryExecutor.cs` — SQL wrapping + plan detection
- `src/SqlAuditedQueryTool.App/Program.cs` — API contract update
- `src/SqlAuditedQueryTool.Core/Models/QueryHistory.cs` — audit flag

**Frontend:**
- `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx` — checkbox
- `src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.tsx` — plan tab
- `src/SqlAuditedQueryTool.App/ClientApp/src/components/ExecutionPlanView.tsx` — NEW
- `src/SqlAuditedQueryTool.App/ClientApp/src/queryApi.ts` — pass flag
- `src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx` — state management
- `src/SqlAuditedQueryTool.App/ClientApp/package.json` — add `html-query-plan`

---

## Open Questions

1. **Estimated vs Actual plans?** This proposal uses actual plans (`SET STATISTICS XML ON`). If Andrew also wants estimated-only plans (no execution), we could add a dropdown: "Off | Estimated | Actual". Start with actual only.
2. **Plan diff?** Future enhancement: compare plans between two executions of the same query. Out of scope for V1.
