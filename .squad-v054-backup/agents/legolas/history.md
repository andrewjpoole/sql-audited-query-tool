# Legolas — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: UI must clearly separate readonly queries from fix suggestions.
- Owns: Chat UI, query interface, results display, user interaction

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-28: Descriptive Simulation Output & Script Preview Updates
- **Change 1:** Enhanced WriteScriptSimulator affected rows display with operation-aware text formatting
  - Added `getOperationType()` helper that parses SQL to detect UPDATE/INSERT/DELETE from statement start
  - Added `formatAffectedRowsText()` to show contextual messages: "1 row updated", "5 rows deleted", "10 rows inserted", etc.
  - Proper singular/plural handling: "1 row" vs "N rows"
  - Falls back to "N rows affected" if operation type unclear
- **Change 2:** Removed database name field from ScriptGeneratorModal
  - Removed `databaseName` state variable and form field (lines 25, 200-209)
  - Removed from `ScriptGenerationRequest` object sent to backend
  - Updated `ScriptGenerationRequest` interface in queryApi.ts to match
- **Change 3:** Updated script preview templates to match Andrew's new conventions
  - **query.sql**: Now shows verification query template with TODO instructions, no @expectedAffectedRowCount declaration
  - **update.sql**: Added `DECLARE @expectedAffectedRowCount INT = N;` at top, replaced `SELECT @@ROWCOUNT` with proper validation using `IF @@ROWCOUNT <> @expectedAffectedRowCount THROW`
  - Removed Database comment line from update.sql header
- **Pattern learned:** Simple SQL parsing via string prefix matching works well for detecting operation types in UI context
- **Files modified:** WriteScriptSimulator.tsx, ScriptGeneratorModal.tsx, queryApi.ts
- **Verification:** TypeScript compilation passed with `npx tsc --noEmit`

### 2026-02-28: SSE Streaming for Chat
- Added Server-Sent Events (SSE) streaming support to ChatPanel for much faster chat responses
- Pattern: Backend sends `stream: true` in request, receives four event types as `data: {json}\n\n`:
  1. `tool_start` — tool execution begins (e.g., "execute_sql_query")
  2. `tool_result` — tool completes with success flag
  3. `text` — LLM's text response content
  4. `done` — final event with full structured data (sessionId, message, suggestion, executedQuery, executedResult)
- Architecture: Added `chatStream()` function to `queryApi.ts` alongside existing `chat()` for backward compat
- SSE parsing: Buffered line-by-line parsing of `data:` prefixed events, handles malformed events gracefully
- UX enhancements: 
  - Stream status indicator below typing dots shows "🔧 Running query..." on tool_start, "✅ Query complete" on tool_result
  - Status auto-clears after 1 second on success
  - Cancel button still works via AbortController signal passed to chatStream
- State management: ChatPanel uses local vars to accumulate streaming data (assistantContent, finalSuggestion, etc.), then creates final ChatMessage only on 'done' event
- CSS: Added `.chat-typing-status` class for subtle status text below typing indicator
- Files modified: `queryApi.ts` (new StreamEvent interface + chatStream function), `ChatPanel.tsx` (updated handleSend to use streaming), `ChatPanel.css` (new status styles)
- Key insight: Streaming allows user to see query execution progress in real-time instead of waiting for full response, significantly improving perceived performance

### 2026-02-28: Chat Cancel Button
- Added cancel button to ChatPanel that replaces the Send button while a request is loading
- Pattern: `AbortController` stored in a `useRef` — created on send, cleared on completion/error, called on cancel click
- `queryApi.ts` `chat()` now accepts an optional `AbortSignal` parameter; when caller provides a signal, the function delegates abort control to the caller and skips creating its own internal controller
- Cancel button repositioned: now appears as a small subtle "✕" inline next to the three pulsing dots in the typing indicator bubble, not near the Send button
- Send button always visible (disabled while loading); cancel is only in the chat message area
- CSS: `.chat-typing-cancel` — minimal no-background button, 11px, turns red on hover
- Error message distinguishes manual cancel ("Request cancelled.") from timeout ("Request timed out...")
- Key files: `queryApi.ts`, `ChatPanel.tsx`, `ChatPanel.css`

### 2026-02-28: Audit Trail UI Controls
- Added optional GitHub Issue # and AzDO Work Item # inputs to the app header for audit context
- Pattern: audit trail fields stored as `number | undefined` state in App.tsx, passed down as props to ChatPanel and through to all API calls
- API params use optional trailing parameters on `executeQuery()` and `chat()` — keeps backward compat, no breaking changes
- CSS: `.audit-trail-inputs` group positioned with `margin-left: auto` to push to far right of header, compact inline layout
- Hid number input spinners via `-moz-appearance: textfield` and `::-webkit-inner-spin-button` for cleaner look
- Key files modified: `queryApi.ts`, `App.tsx`, `App.css`, `ChatPanel.tsx`
- Backend DTOs expect camelCase: `gitHubIssueNumber`, `azDoWorkItemId` — matches JSON serialization convention

## Foundation Work Summarization (2026-02-22 to 2026-02-24)

This section consolidates early foundational work before recent focused features.

**Frontend Stack & Architecture (2026-02-22):**
- Vite 7 + React 19 + TypeScript in `src/SqlAuditedQueryTool.App/ClientApp/`
- Monaco editor (`@monaco-editor/react`) with SQL support, dark theme, 6 context menu commands (Insert Date/GUID/GETDATE/NEWID/Wrap SELECT/Toggle Comment)
- Three-column layout: SchemaTreeView (left) | Editor + QueryResults (center) | ChatPanel (right)
- All panels persistent/always-visible (no toggles after 2026-02-24 redesign)
- SPA integration via `Microsoft.AspNetCore.SpaServices.Extensions` v9.x; Vite dev server on 5173 proxies to .NET on 5001

**Core Features Implemented (2026-02-22 through 2026-02-24):**
1. **Query Execution:** QueryResults.tsx renders data tables with sortable columns, row counts, execution times
2. **AI Chat Integration:** ChatPanel.tsx with message bubbles, SQL code block detection/insertion, read vs fix query visual separation (green execute button vs orange warning banner)
3. **Query History:** QueryHistory.tsx sidebar showing session history with timestamps, row counts, 👤 user vs 🤖 AI source badges, click-to-reload functionality
4. **Schema Browser:** SchemaTreeView.tsx with full schema tree (Schema → Tables → Columns/Indexes/Foreign Keys), search filter, right-click context menu for quick SQL patterns (SELECT TOP 1000, COUNT, WHERE templates)
5. **Chat Sessions:** useChatHistory.ts hook manages localStorage persistence of chat conversations, ChatPanel shows session list with create/delete/load functionality
6. **Monaco Completions:** TabbedSqlEditor.tsx registers completion provider calling `/api/completions/schema`, backend returns all context-appropriate items, Monaco handles fuzzy filtering client-side
7. **Tabbed Editor:** Multiple SQL query tabs with F7/F8 shortcuts (Execute / Run Selection), context menu support for SQL operations

**Major Bug Fixes & Refinements (2026-02-23 to 2026-02-24):**
- **Infinite Loop Fix:** Chat history had useEffect recursion due to callback reference changing; fixed by moving session list into ChatPanel and using useCallback for stable references
- **Layout Simplification:** Removed toggleable panels; all sidebars now persistent (Andrew preference). Chat History and Query History are always-visible
- **Resizable Panes:** Added drag handles for QueryResults pane vertical resize (later simplified to always full-height after Andrew's persistent panel preference)
- **Monaco Keybindings:** Added F5 (Format), F7 (Execute), F8 (Run Selection), Ctrl+/ (Comment), Ctrl+Space (Autocomplete)
- **Button Styling Consistency:** Made Run Selection button match Execute button visual prominence (both primary actions)
- **Connection Status Removal:** Removed redundant status bar indicator (schema visibility already indicates connection)

**API Contract with Backend (Summarized):**
- `POST /api/query/execute` — execute query, return results + metadata
- `POST /api/chat` — chat with LLM, optional schema context, streaming support
- `POST /api/query/suggest` — natural language to SQL suggestion
- `GET /api/schema` — schema metadata (tables, columns, indexes, FK)
- `GET /api/completions/schema` — autocomplete suggestions for prefix + context
- `POST /api/simulation/*` — Write Script Simulator endpoints (added in recent Phase 1)

**Key Patterns & Learnings:**
- Plain CSS with CSS custom properties works well for dark theme without framework overhead (bundle stays <75KB)
- Monaco requires careful ref/dispose patterns to prevent memory leaks
- Persistent sidebars are preferred by users over toggle-based hiding
- Visual separation of read vs write operations is critical for safety (green vs orange/amber themes)
- Chat history with localStorage persistence enables multi-turn conversations without server state

**Current State (as of 2026-02-28):**
- All core UI features implemented and stable
- Write Script Simulator Phase 1 MVP complete (amber theme mode toggle, WriteScriptSimulator.tsx, ScriptGeneratorModal.tsx)
- Ready for further enhancements (audit logging, advanced filtering, export)

---

---

### 2026-02-28T21:45:47Z: Write Script Simulator Frontend — Phase 1 MVP Complete
- **New feature:** Write Script Simulator mode — full simulation + sql-script-runner script generation UI
- **Architecture decision:** Separate app mode toggle (Query vs Simulator) — visually distinct with amber/orange theme
- **Components created:**
  - WriteScriptSimulator.tsx — main simulator component with Monaco SQL editor, simulation button, results display
  - ScriptGeneratorModal.tsx — modal for generating sql-script-runner scripts with repository selection, work item ID, purpose, preview
  - CSS files: WriteScriptSimulator.css and ScriptGeneratorModal.css with amber theme (--sim-accent: #d97706)
- **API additions in queryApi.ts:**
  - SimulationResult, ScriptRepository, ScriptGenerationRequest, ScriptGenerationResult types
  - simulateQuery(), getSimulationRepositories(), generateScripts() functions
- **UI patterns:**
  - Amber/orange theme throughout simulator (⚠️ SIMULATION MODE banner, amber buttons, warning colors)
  - Estimated affected rows with color coding: green (≤10), amber (11-100), red (>100)
  - Reuses ExecutionPlanView component for plan visualization
  - Script preview shows both query.sql and update.sql with proper headers and formatting
- **App.tsx changes:**
  - Added mode toggle in header: Query | ⚠️ Write Simulator
  - Dynamic header badge: "Read-Only" vs "⚠️ Simulation"
  - Conditional rendering: Query mode shows full 3-column layout, Simulator mode shows SchemaTreeView + WriteScriptSimulator
- **CSS additions to App.css:**
  - .app-mode-toggle — button group for mode switching
  - .mode-btn with --active and --simulator variants
  - .app-header-badge--simulator for amber badge styling
- **Key architectural insight:** Simulator is self-contained — all state lives in WriteScriptSimulator, App.tsx only handles mode toggle
- **Visual separation enforced:** Users immediately know they're in simulation mode via amber theme, banner, and badge
- **Coordination:** Backend API contract defined by Samwise; frontend ready for integration
- **File paths:**
  - Components: src/SqlAuditedQueryTool.App/ClientApp/src/components/WriteScriptSimulator.tsx & ScriptGeneratorModal.tsx
  - API client: src/SqlAuditedQueryTool.App/ClientApp/src/api/queryApi.ts
  - Main app: src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx & App.css
