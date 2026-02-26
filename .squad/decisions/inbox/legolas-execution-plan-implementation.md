# Execution Plan Frontend Implementation — Completed

**Date:** 2026-02-24  
**By:** Legolas (Frontend Specialist)  
**Status:** COMPLETED  

---

## Summary

Successfully implemented the frontend portion of the execution plan feature according to the architecture specification in `gandalf-execution-plan-feature.md`.

---

## Implementation Details

### 1. ✅ Installed `html-query-plan` Package
- Installed version `2.6.1` via npm
- Package provides visual rendering of SQL Server execution plans
- Zero dependencies, lightweight library

### 2. ✅ Added "Show Plan" Checkbox to TabbedSqlEditor
**Location:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx`

**Changes:**
- Added checkbox to `editor-toolbar` between Execute/Run Selection buttons and tabs
- Label: "Show Plan"
- State managed in component with `useState`
- Persisted to `localStorage` with key `showExecutionPlan`
- Defaults to `false` (unchecked)
- Callback `onShowPlanChange` notifies parent component when checkbox changes

**Styling:** Added checkbox styling in `TabbedSqlEditor.css`:
- Hover effects
- Proper spacing and alignment
- Matches existing toolbar button style

### 3. ✅ Updated API Layer
**Location:** `src/SqlAuditedQueryTool.App/ClientApp/src/api/queryApi.ts`

**Changes:**
- Updated `QueryResult` interface to include optional `executionPlanXml?: string | null`
- Updated `executeQuery()` function signature to accept optional `includeExecutionPlan` parameter (defaults to `false`)
- API call passes `includeExecutionPlan` in request body: `{ sql, includeExecutionPlan }`

### 4. ✅ Updated App.tsx State Management
**Location:** `src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx`

**Changes:**
- Added `includeExecutionPlan` state variable
- Created `handleShowPlanChange` callback to update state when checkbox changes
- Passed callback to `TabbedSqlEditor` via `onShowPlanChange` prop
- Updated all query execution functions to pass `includeExecutionPlan` flag:
  - `handleExecute()`
  - `handleExecuteSelection()`
  - `handleInsertAndExecute()`
- Added console logging when execution plan XML is received

### 5. ✅ Created ExecutionPlanView Component
**Location:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/ExecutionPlanView.tsx`

**Features:**
- Two view modes: Visual (default) and Raw XML
- Toggle button to switch between views
- "Copy XML" button to copy plan XML to clipboard
- Visual plan rendered using `html-query-plan` library
- Uses `useRef` and `useEffect` to render plan: `QP.showPlan(containerRef.current, planXml)`
- Error handling for plan rendering failures
- Container cleared before each render to prevent duplicates

**Styling:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/ExecutionPlanView.css`
- Imports `html-query-plan` CSS: `@import 'html-query-plan/css/qp.css'`
- Custom toolbar styling
- Scrollable container for large plans
- Raw XML view with monospace font and syntax preservation
- Theme overrides to match application dark theme
- Error message styling

### 6. ✅ Updated QueryResults Component
**Location:** `src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.tsx`

**Changes:**
- Added import for `ExecutionPlanView` component
- Added `activeTab` state to manage which tab is displayed (type: `number | 'execution-plan'`)
- Added `hasExecutionPlan` derived state: `result?.executionPlanXml != null`
- Implemented tab navigation UI:
  - Tabs only shown when multiple result sets OR execution plan exists
  - "Result Set N" tabs for each result set
  - "📊 Execution Plan" tab when plan data is present
  - Active tab highlighted
- Tab content rendering logic:
  - Shows `ExecutionPlanView` when plan tab is active
  - Shows result sets in stacked view for single result set without plan
  - Shows active result set tab when multiple tabs exist

**Styling:** Updated `src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.css`
- Added `.qr-tab-content` class for tab content container
- Added `.qr-content-stacked` class for stacked result sets
- Proper flex layout for scrollable content

### 7. ✅ Build Verification
- Ran `npm run build` successfully
- No TypeScript compilation errors
- No ESLint warnings
- Build output: 361.96 kB JS (103.08 kB gzipped), 32.37 kB CSS (5.62 kB gzipped)
- Build includes execution plan assets: `qp_icons-CBXjZmhW.png` (81.22 kB)

---

## Files Modified

**New Files:**
1. `src/SqlAuditedQueryTool.App/ClientApp/src/components/ExecutionPlanView.tsx`
2. `src/SqlAuditedQueryTool.App/ClientApp/src/components/ExecutionPlanView.css`

**Modified Files:**
1. `src/SqlAuditedQueryTool.App/ClientApp/package.json` — added `html-query-plan` dependency
2. `src/SqlAuditedQueryTool.App/ClientApp/src/api/queryApi.ts` — API interface updates
3. `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx` — checkbox + state
4. `src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.css` — checkbox styling
5. `src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.tsx` — tabs + plan view
6. `src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.css` — tab styling
7. `src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx` — state management + callbacks

---

## Testing Checklist

**Ready for Testing (after backend implementation):**
- [ ] Checkbox defaults to unchecked
- [ ] Checkbox state persists to localStorage
- [ ] Unchecked: no plan in response, no plan tab shown
- [ ] Checked: plan in response, plan tab appears
- [ ] Visual plan renders correctly with operator nodes
- [ ] Toggle between Visual and XML views works
- [ ] Copy XML button copies to clipboard
- [ ] Plan view is scrollable for large plans
- [ ] Multiple result sets + plan shows all tabs
- [ ] Single result set + plan shows plan tab
- [ ] Plan-only queries (no result sets) show just plan tab
- [ ] F7 (Execute) and F8 (Run Selection) both respect checkbox state

---

## Next Steps

**Awaiting Backend Implementation (Samwise):**
1. Add `IncludeExecutionPlan` to `QueryRequest` model
2. Wrap SQL with `SET STATISTICS XML ON/OFF` when flag is true
3. Detect and extract execution plan from last result set
4. Populate `QueryResult.ExecutionPlanXml`
5. Update `/api/query/execute` endpoint to accept and return plan fields

**Once Backend is Complete:**
- Integration testing with real SQL Server queries
- Verify plan XML format is compatible with `html-query-plan`
- Test with complex queries (joins, subqueries, stored procedures)
- Verify performance with large plans (50-200KB XML)

---

## Design Decisions

### Checkbox Placement
Placed checkbox in toolbar after Execute/Run Selection buttons, before tab controls. This makes it visible and easily accessible without cluttering the interface.

### Default State
Checkbox defaults to `false` (unchecked) to avoid performance overhead unless user explicitly requests execution plans.

### LocalStorage Persistence
User preference persists across sessions via `localStorage` key `showExecutionPlan`, providing a consistent experience.

### Tab Visibility Logic
- Single result set, no plan → No tabs (stacked view)
- Multiple result sets, no plan → Result set tabs only
- Single result set + plan → Result set tab + plan tab
- Multiple result sets + plan → All result set tabs + plan tab
- No result sets + plan → Plan tab only

### Visual vs XML Toggle
Default to visual plan (more user-friendly), with easy toggle to XML for debugging or export.

### Error Handling
If visual plan rendering fails (malformed XML, library issue), show error message and suggest viewing XML mode.

---

## Architecture Compliance

✅ All requirements from `gandalf-execution-plan-feature.md` implemented:
- `html-query-plan` npm package installed
- Checkbox in toolbar with "Show Plan" label
- State persisted to localStorage
- API accepts `includeExecutionPlan` parameter
- `QueryResult` includes `executionPlanXml` field
- `ExecutionPlanView` component with Visual + XML views
- Toggle button and Copy XML button
- Execution Plan tab in results panel
- CSS styling for plan view
- Scrollable container for large plans

✅ No deviations from specification

---

## Performance Considerations

**Frontend Impact:**
- `html-query-plan` is lightweight (zero dependencies)
- Plan rendering is lazy (only when plan tab is active)
- Visual plan uses efficient SVG rendering
- Raw XML uses simple `<pre>` tag (minimal overhead)
- Toggle state stored in component (no backend calls)

**Build Impact:**
- Added 81KB for plan icons (gzipped in production)
- Total bundle size increase: ~10KB gzipped
- No impact on queries without execution plans

---

## Known Limitations

1. **Backend Dependency:** Feature requires backend to implement plan extraction (Samwise's task)
2. **SQL Server Only:** Execution plans are SQL Server-specific (`SET STATISTICS XML ON`)
3. **Plan Size:** Very large plans (>500KB XML) may cause browser slowdown
4. **Browser Clipboard API:** "Copy XML" requires secure context (HTTPS or localhost)

---

## Future Enhancements (Out of Scope)

- Plan comparison (diff two execution plans)
- Estimated vs Actual plan toggle
- Plan export to file (.sqlplan format)
- Plan statistics summary (total cost, estimated rows, etc.)
- Highlighting expensive operators (>10% of total cost)
