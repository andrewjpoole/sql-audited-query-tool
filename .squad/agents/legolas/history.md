# Legolas — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: UI must clearly separate readonly queries from fix suggestions.
- Owns: Chat UI, query interface, results display, user interaction

## Learnings
<!-- Append new learnings below this line -->

### 2026-03-06: Schema Validation Auto-Retry — Frontend Event Handling
- **Change:** Added schema_retry SSE event handling in ChatPanel.tsx
- **Implementation:** Backend now sends schema_retry events during schema validation failures (before showing raw validation errors to user)
- **Event Type:** Added to `StreamEvent` union with fields: `attempt`, `maxAttempts`
- **UI Behavior:** ChatPanel shows progress status: "🔄 Fixing schema issues (attempt N/M)..." 
- **Backend Integration:** Samwise implemented retry loop in Program.cs (streaming path sends SSE events)
- **Pattern:** SSE events provide real-time feedback during multi-step backend operations

## Foundation Work Summarization (2026-02-22 to 2026-03-05)

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
### 2026-03-06: Schema Validation Auto-Retry — Frontend Event Handling
- **Change:** Added schema_retry SSE event handling in ChatPanel.tsx
- **Implementation:** Backend now sends schema_retry events during schema validation failures (before showing raw validation errors to user)
- **Event Type:** Added to StreamEvent union with fields: ttempt, maxAttempts
- **UI Behavior:** ChatPanel shows progress status: "🔄 Fixing schema issues (attempt N/M)..." 
- **Backend Integration:** Samwise implemented retry loop in Program.cs (streaming path sends SSE events)
- **No UI Changes:** SuggestionCard.tsx already handles remaining schemaWarnings — no modifications needed
- **Pattern:** SSE events provide real-time feedback during multi-step backend operations

### 2026-03-06T13:19:00Z: Chat & Simulator Integration Fixes & Button Routing
- **Chat always visible in both modes** — Added `appMode` prop to ChatPanel, conditional rendering of mode-specific features
- **Send to Simulator flow** — SuggestionCards now have "🔬 Send to Simulator" button; App.tsx holds `simulatorSql` state; WriteScriptSimulator accepts `externalSql` via useEffect
- **Deduplication pattern** — When SuggestionCard present, `stripSqlCodeBlocks()` removes code fences from assistant text; `hasSuggestion` flag skips extractSqlBlocks rendering
- **Copy button** — All SuggestionCards have "📋 Copy" button using `navigator.clipboard.writeText()` with 2-second feedback
- **Write query button routing** — Write operations (UPDATE/INSERT/DELETE) now show "🔬 Run in Simulator" instead of "📝 Insert & Execute"
  - Backend: Added `isReadOnly` to suggestion serialization in Program.cs (both streaming SSE and non-streaming response paths)
  - Frontend: Added `isReadOnly?: boolean` to QuerySuggestion interface
  - Logic: SuggestionCard checks `isReadOnly !== false` for Insert & Execute, `isReadOnly === false` for Run in Simulator
  - Backward compat: undefined isReadOnly defaults to true (readonly/Insert & Execute)
- **Files touched:** App.tsx, ChatPanel.tsx, ChatPanel.css, WriteScriptSimulator.tsx, Program.cs, queryApi.ts
- **Build status:** npm build ✅, dotnet build ✅
- **Impact:** No breaking changes; WriteScriptSimulator `externalSql` prop optional; ChatPanel props `appMode` and `onSendToSimulator` required

### 2026-07-24: Write Query → Simulator Button Routing
- **Change:** Write operations (UPDATE/INSERT/DELETE) now show "🔬 Run in Simulator" instead of "▶ Insert & Execute" in SuggestionCard
- **Backend:** Added `isReadOnly` to suggestion serialization in Program.cs (both streaming SSE and non-streaming response paths). `SuggestedQuery` model already had `IsReadOnly`.
- **Frontend interface:** Added `isReadOnly?: boolean` to `QuerySuggestion` in queryApi.ts
- **Frontend logic:** SuggestionCard read-query section now checks `suggestion.isReadOnly !== false` for Insert & Execute, and `suggestion.isReadOnly === false` for Run in Simulator
- **Pattern:** `isReadOnly !== false` (true or undefined) preserves backward compatibility — old responses without the field still get Insert & Execute
- **Files touched:** Program.cs (2 serialization sites), queryApi.ts, ChatPanel.tsx
- **Build status:** npm build ✅, dotnet build ✅
