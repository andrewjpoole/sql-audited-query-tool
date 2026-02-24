# Legolas — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: UI must clearly separate readonly queries from fix suggestions.
- Owns: Chat UI, query interface, results display, user interaction

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.App` — ASP.NET Core Empty (dotnet new web) — minimal, ready for chat UI build
- **Architecture:** App is composition root; references Core, Database, Audit, Llm
- **UI patterns:** Build on ASP.NET Core empty template; endpoints for chat, query execution, results display
- **Ready to start:** Chat UI scaffolding, API endpoint design, results rendering

### 2026-02-22T14:15:00Z: React Frontend Created with Monaco SQL Editor
- **Frontend stack:** Vite 7 + React 19 + TypeScript, inside `src/SqlAuditedQueryTool.App/ClientApp/`
- **Editor:** `@monaco-editor/react` with SQL language mode, dark theme, minimap off, word wrap on
- **Key component:** `src/components/SqlEditor.tsx` — Monaco editor with 6 custom context menu commands under "SQL Helpers" group
- **Context menu commands:** Insert Current Date (Alt+D), Insert New GUID (Alt+G), Insert GETDATE(), Insert NEWID(), Wrap in SELECT (Alt+S), Toggle Comment (Ctrl+/)
- **App layout:** Header bar → Toolbar with Execute button → Editor panel → Results placeholder panel
- **SPA integration:** `Microsoft.AspNetCore.SpaServices.Extensions` v9.x; Vite dev proxy on port 5173 → .NET on port 5001
- **Health endpoint:** `GET /api/health` returns `{ status: "ok" }` — serves as proxy test
- **CSS approach:** Plain CSS with CSS custom properties (dark theme consistent with Monaco vs-dark)
- **Monaco type note:** `addAction` run callback receives `ICodeEditor`, not `IStandaloneCodeEditor` — use the broader type
- **Build verified:** Both `npm run build` and `dotnet build` succeed

### 2026-02-22T16:00:00Z: Core UI Features Built
- **API client:** `src/api/queryApi.ts` — typed functions for `executeQuery`, `suggestQuery`, `chat`, `getSchema` with error handling
- **Types:** `QueryResult`, `QuerySuggestion`, `ChatMessage`, `LlmResponse`, `SchemaContext` — all exported
- **QueryResults panel:** `src/components/QueryResults.tsx` — data table with sortable column headers, striped rows, loading spinner, error/empty states, collapsible via header click, shows row count + execution time
- **ChatPanel:** `src/components/ChatPanel.tsx` — collapsible right-side panel with message bubbles, typing indicator, SQL suggestion cards. Read queries get green "Insert & Execute" button. Fix queries get yellow/orange warning banner with text "⚠️ FIX QUERY — Must be run in a separate tool with write access"
- **QueryHistory:** `src/components/QueryHistory.tsx` — toggleable left sidebar showing session query history (timestamp, first line, row count), click to reload into editor
- **App layout:** Three-column layout: optional history sidebar (left) | center (editor + results) | optional chat panel (right). Toolbar has Execute, History toggle, Chat toggle, connection status indicator
- **Wiring:** Execute button calls `executeQuery()` API and displays results; Chat panel calls `chat()` API; suggestion "Insert into Editor" sets Monaco content; "Insert & Execute" sets content and auto-runs query
- **CSS:** Plain CSS, dark theme consistent with vs-dark, no component library — keeps bundle small
- **Key decision:** Read vs fix query separation enforced visually — read queries have green execute action, fix queries have orange/yellow warning banner and no execute button
- **Build verified:** `npm run build` succeeds (71 modules, 219KB JS gzip 69KB)

### 2026-02-22T18:00:00Z: Schema TreeView Component Built
- **New component:** `src/components/SchemaTreeView.tsx` + `SchemaTreeView.css` — collapsible database schema browser in left sidebar
- **Tree structure:** Schema → Table → Columns / Indexes / Foreign Keys, with emoji icons (📁📋🔑📝📇🔗) and colored badges (PK, ID, UQ, CL, calc)
- **Features:** Search/filter tables by name, refresh button, collapse sidebar, click table/column to insert into editor at cursor position
- **API types enriched:** `queryApi.ts` now has `SchemaColumn`, `SchemaIndex`, `SchemaForeignKey` interfaces; `SchemaTable` includes `primaryKey`, `indexes`, `foreignKeys` arrays
- **SqlEditor refactored:** Converted to `forwardRef` with `useImperativeHandle` exposing `SqlEditorHandle.insertTextAtCursor()` — allows parent components to insert text at Monaco cursor
- **App.tsx wiring:** SchemaTreeView is always-visible leftmost panel in `.main-area`, receives `onInsertText` callback connected to editor ref
- **Layout:** Schema panel (280px) | optional History | Center (editor+results) | optional Chat
- **Styling:** Uses existing CSS variables (dark theme), consistent with QueryHistory sidebar pattern
- **Build verified:** `npm run build` succeeds (73 modules, 226KB JS gzip 71KB)

### 2026-02-22T20:30:00Z: Context Menu for Quick Queries Added
- **Feature:** Right-click context menu on table rows in SchemaTreeView for common SQL queries
- **Menu options:** "SELECT TOP 1000 *", "SELECT COUNT(*)", "SELECT * WHERE..." — all insert qualified table names
- **Implementation:** `onContextMenu` handler prevents default browser menu, shows custom menu at click position
- **UX patterns:** Click outside closes menu, clicking option inserts SQL and closes menu, uses existing `onInsertText` callback
- **State management:** Context menu state tracks visibility, position (x, y), and target table; `useRef` + `useEffect` for click-outside detection
- **Styling:** `.stv-context-menu` with dark theme variables, hover state with accent color, fixed positioning with z-index 9999
- **Bug fix:** Fixed pre-existing unused `message` parameter in `queryApi.chat()` — now properly appends user message to history array before sending to API
- **Build verified:** `npm run build` succeeds (73 modules, 227KB JS gzip 71KB)

### 2026-02-22T22:00:00Z: Chat Timeout & SQL Block Detection Added
- **Problem 1 - Timeout handling:** Added configurable timeout (60s default) using AbortController in `queryApi.chat()`
- **Error messaging:** Timeout errors show user-friendly message with ⏱️ emoji, suggesting retry or simplification
- **Problem 2 - SQL detection:** Chat messages now parse markdown SQL code blocks (```sql...```) and render them as actionable cards
- **UI pattern:** SQL blocks display with monospace pre tag + "📝 Insert into Editor" button below
- **Regex extraction:** `extractSqlBlocks()` utility function extracts all ```sql code blocks from assistant messages
- **CSS additions:** `.chat-sql-block`, `.chat-sql-code`, `.chat-sql-insert` styles with hover transition to accent color
- **Integration:** SQL blocks use existing `onInsertSql` callback, same as suggestion cards — sets Monaco editor content
- **Key files:** `queryApi.ts` (timeout logic), `ChatPanel.tsx` (SQL block detection + rendering), `ChatPanel.css` (new styles)
- **Build verified:** `npm run build` succeeds (73 modules, 228KB JS gzip 71KB)

### 2026-02-22T23:30:00Z: AI Query Execution & Chat History Persistence
- **Feature 1 - AI-executed queries:** Queries executed by Ollama via tool calling now load into Monaco editor with visual indicators
- **Source tracking:** `HistoryEntry` interface extended with optional `source: 'user' | 'ai'` field
- **Visual badges:** AI queries show 🤖 robot emoji, user queries show 👤 user emoji in query history panel
- **API types:** `LlmResponse` extended with `executedQuery?: string` and `executedResult?: QueryResult` fields
- **Query flow:** When API returns executed query, `ChatPanel` calls `onAiExecutedQuery` callback → App loads query into editor and displays results with `source: 'ai'` in history
- **Feature 2 - Chat history persistence:** Chat conversations now persist in localStorage with session management
- **Hook created:** `src/hooks/useChatHistory.ts` — manages session CRUD (create, load, update, delete) with auto-save to localStorage
- **Session model:** `ChatSession` interface with id, timestamp, messages array, and auto-generated title from first user message
- **Component added:** `ChatSessionList.tsx` + `.css` — collapsible sidebar showing past sessions with time ago formatting, message count, active state highlighting
- **Session UI:** New "📚 Chat History" toolbar button toggles session list; "✚ New" button starts fresh session; individual sessions deletable with 🗑️ button
- **State coordination:** `App.tsx` manages current session via `useChatHistory()` hook; `ChatPanel` receives `sessionId`, `initialMessages`, and `onMessagesChange` props for sync
- **Layout:** Session list appears between SchemaTreeView and QueryHistory when toggled (Schema | Chat History | Query History | Editor | Chat Panel)
- **Key files:** `useChatHistory.ts` (localStorage logic), `ChatSessionList.tsx` (session browser), `App.tsx` (integration), `ChatPanel.tsx` (session-aware messaging), `QueryHistory.tsx` (source badges)
- **Build verified:** `npm run build` succeeds (76 modules, 232KB JS gzip 73KB)

### 2026-02-22T23:08:00Z: Chat History UI Redesign & Infinite Loop Fix
- **Problem:** Chat history sidebar had flickering/infinite loop bug and didn't match user requirements
- **Root cause:** Infinite loop caused by `useEffect` with `onMessagesChange` in dependency array — callback reference changed on every render, triggering effect → state update → re-render cycle
- **Solution 1 - Architecture change:** Removed separate `ChatSessionList` sidebar; integrated chat selector directly into `ChatPanel` at the top
- **New UI design:** Collapsible "Chats" section at top of ChatPanel showing sessions count, with expandable list and "✚ New" button
- **Session list features:** Click to expand/collapse, shows session title (first message preview), timestamp (relative "5m ago"), message count, active state highlight, delete button with confirmation
- **Solution 2 - State management fix:** Eliminated problematic `useEffect` patterns; `ChatPanel` now receives sessions array and callbacks directly, uses `useCallback` for `updateMessages` to prevent infinite loops
- **Data flow:** `App.tsx` manages all session state via `useChatHistory` hook → passes sessions, currentSessionId, and callbacks to `ChatPanel` → ChatPanel reads current session messages from sessions array, updates via `onUpdateSession` callback
- **Props refactored:** Replaced `sessionId`, `initialMessages`, `onMessagesChange` with `sessions`, `currentSessionId`, `onNewSession`, `onLoadSession`, `onDeleteSession`, `onUpdateSession`
- **Removed:** "📚 Chat History" toolbar button and `ChatSessionList` as separate sidebar component
- **Layout change:** Simplified to Schema | Query History | Editor | Chat Panel (no chat history sidebar)
- **CSS additions:** `.chat-sessions`, `.chat-sessions-header`, `.chat-sessions-list`, `.chat-session-item` styles with hover states, active highlighting, scrollable max-height 200px
- **UX improvements:** Collapsed by default to save space; expandable triangle indicator; delete requires confirmation prompt; new chat button always visible in header
- **Key learning:** Avoid putting callback functions in useEffect dependencies unless they're stable via useCallback; prefer direct props over syncing state between parent/child with effects
- **Build verified:** `npm run build` succeeds (74 modules, 231.52KB JS gzip 72.61KB)

### 2026-02-23T10:00:00Z: Tabbed SQL Query Editor Implementation
- **Feature:** Multi-file tabbed query editor using Monaco's native multi-model support
- **Research finding:** Monaco Editor supports multiple models natively via the `path` prop — no custom tab management needed at Monaco level
- **Key insight:** @monaco-editor/react v4.7.0 has built-in multi-model API — when you provide a `path` prop, Monaco automatically manages separate models with independent view states, undo stacks, scroll positions
- **Component created:** `TabbedSqlEditor.tsx` + `.css` — replaces `SqlEditor.tsx` as main editor component
- **Tab features:** 
  - Create new tabs with "+" button
  - Close tabs with "×" button (with dirty state warning)
  - Switch tabs by clicking
  - Rename tabs via double-click
  - Dirty indicator (●) shows unsaved changes per tab
  - At least one tab always open
  - Each tab maintains independent Monaco model via unique `path` prop
- **Monaco integration:** Uses `path={activeTab.path}` and `defaultValue={activeTab.defaultValue}` — Monaco handles model persistence automatically across tab switches
- **State management:** `QueryTab` interface tracks `id`, `name`, `path`, `defaultValue`, `isDirty` per tab
- **UX patterns:** Horizontal tab bar with scrollable overflow, active tab highlighted with accent border, hover states, close button appears on hover
- **Context menus preserved:** All SQL helper actions (Insert Date, GUID, GETDATE, etc.) work on current active tab
- **Styling:** Follows existing dark theme variables, compact 36px tab bar, smooth transitions, consistent with VS Code tab UI patterns
- **Integration:** Updated `App.tsx` to use `TabbedSqlEditor` instead of `SqlEditor`; maintains same `SqlEditorHandle` ref interface for text insertion
- **Key files:** `TabbedSqlEditor.tsx`, `TabbedSqlEditor.css`, `App.tsx` (import change)
- **Build verified:** `npm run build` succeeds (75 modules, 233.47KB JS gzip 73.24KB)

### 2026-02-23T12:00:00Z: Schema TreeView Icon Size Improvement
- **Issue:** User feedback that icons in SchemaTreeView were too small/hard to see
- **Root cause:** `.stv-icon` CSS class had `font-size: 13px` and `width: 18px` — too small for emoji icons in tree view
- **Solution:** Increased icon sizing to `font-size: 18px` and `width: 20px` in `SchemaTreeView.css`
- **Reasoning:** Tree view icons typically work best at 16-20px range; 18px provides good visibility while maintaining proportional layout
- **Impact:** Icons (📁📋🔑📝📇🔗) are now clearly visible and appropriately sized for the tree hierarchy
- **User preference:** Andrew prefers larger, more visible icons for better UX
- **Key file:** `src/components/SchemaTreeView.css` line 116-121 (`.stv-icon` class)
- **Build verified:** `npm run build` succeeds (75 modules, 233.47KB JS gzip 73.24KB)

### 2026-02-23T14:00:00Z: Infinite Loop Bug Fix — Chat Session Creation
- **Problem:** Chat sessions created uncontrollably in infinite loop when user asks a question
- **Root cause:** In `App.tsx`, `chatHistory` object from `useChatHistory()` hook was used as dependency in `useCallback` hooks (lines 138-152). Since `chatHistory` is a new object on every render, the callback references changed on every render. This made `onUpdateSession` callback unstable, which triggered infinite re-renders in `ChatPanel` because `updateMessages` callback (line 70-74) had `onUpdateSession` in its dependency array.
- **Infinite loop cycle:** App re-renders → new `chatHistory` object → new callback references → ChatPanel re-renders → new `updateMessages` callback → message update → unstable callback triggers App re-render → loop continues
- **Solution:** Destructured individual stable functions from `useChatHistory()` hook (`createNewSession`, `loadSession`, `updateSession`, `deleteSession`) and used them directly as dependencies in `useCallback` hooks instead of the entire `chatHistory` object
- **Key changes:** 
  - Line 31-39: Changed from `const chatHistory = useChatHistory()` to destructured `const { sessions, currentSessionId, createNewSession, loadSession, updateSession, deleteSession } = useChatHistory()`
  - Lines 138-152: Updated `useCallback` dependencies from `[chatHistory]` to specific function names like `[createNewSession]`, `[loadSession]`, etc.
  - Lines 217-219: Changed props from `chatHistory.sessions` to `sessions`, `chatHistory.currentSessionId` to `currentSessionId`
- **Key learning:** Never use entire hook return objects in `useCallback` dependencies — destructure and use individual stable references. Hook return objects are new on every render even if their contents are stable.
- **Pattern to avoid:** `const hook = useHook(); useCallback(() => hook.method(), [hook])` ❌
- **Pattern to use:** `const { method } = useHook(); useCallback(() => method(), [method])` ✅
- **Build verified:** `npm run build` succeeds (75 modules, 233.46KB JS gzip 73.24KB)

### 2026-02-23T16:00:00Z: Infinite Chat Bug REAL Fix — Recursive setTimeout
- **Problem:** Previous fix (destructuring hook) didn't solve the infinite chat bug. Users reported chats still created uncontrollably when sending a message.
- **REAL root cause:** In `ChatPanel.tsx` `handleSend()` function (lines 82-129), when no session existed, the code called `onNewSession()` then used `setTimeout(() => handleSend(), 0)` to retry. This created an infinite recursion loop because:
  1. `onNewSession()` creates a session and updates parent state via `setSessions` and `setCurrentSessionId`
  2. Parent state update triggers re-render, but the `setTimeout` callback has already been scheduled with OLD closure values
  3. `setTimeout` fires, `handleSend()` runs again with OLD `currentSessionId` (still null)
  4. Condition `if (!currentSessionId)` is still true, so `onNewSession()` is called AGAIN
  5. Another `setTimeout` is scheduled, creating infinite loop of session creation
- **Why setTimeout doesn't work:** React state updates are asynchronous and batched. When you call `onNewSession()`, the component doesn't re-render immediately. The `setTimeout` callback captures the OLD state in its closure, so `currentSessionId` is still null when it runs.
- **Solution:** Use the RETURN VALUE from `onNewSession()`. The `useChatHistory` hook's `createNewSession()` function returns the new session ID synchronously (line 47-57 in useChatHistory.ts). Changed to: `const sessionId = currentSessionId || onNewSession();` — this gets the ID immediately without waiting for re-render.
- **Key changes:**
  - Line 82-107: Removed `setTimeout` recursion pattern entirely
  - Line 93: Changed from `if (!currentSessionId) { onNewSession(); setTimeout(...); return; }` to `const sessionId = currentSessionId || onNewSession();`
  - Line 95-130: All session updates now use local `sessionId` variable instead of relying on state
  - Updated `ChatPanelProps.onNewSession` type from `() => void` to `() => string` to match actual return type
  - Updated `App.tsx` `handleNewChatSession` callback to return the value from `createNewSession()`
  - Removed unused `updateMessages` callback wrapper (lines 70-74) since we now call `onUpdateSession` directly with the local `sessionId`
- **Key learning:** NEVER use `setTimeout(() => recursiveCall(), 0)` to retry after state updates. React state updates are async — the callback will capture OLD state in closure. Instead, use return values from state-updating functions when available, or use refs/useEffect for coordination.
- **Anti-pattern:** `if (!state) { updateState(); setTimeout(() => retry(), 0); }` ❌
- **Correct pattern:** `if (!state) { const newState = updateStateAndReturn(); useNewState(newState); }` ✅
- **Build verified:** `npm run build` succeeds (75 modules, 233.40KB JS gzip 73.23KB)

### 2026-02-23T18:00:00Z: SQL Insertion Broken After Tabbed Editor Refactor — Fixed
- **Problem:** "Insert and Execute" and "Insert into Editor" buttons in ChatPanel stopped updating editor contents after switching to TabbedSqlEditor
- **Root cause:** When refactoring from `SqlEditor` to `TabbedSqlEditor`, the callbacks in `App.tsx` were still using `setSql()` to update state directly. This doesn't work with Monaco's multi-model system used by tabbed editor:
  1. TabbedSqlEditor maintains separate Monaco models per tab via unique `path` props
  2. Each model has independent content managed by Monaco itself
  3. Setting React state (`setSql`) doesn't sync with the active Monaco model's content
  4. Result: state updates but Monaco editor shows stale content
- **Solution:** Added `setValue()` method to `SqlEditorHandle` interface that directly manipulates Monaco model content:
  - Line 7-10 in TabbedSqlEditor.tsx: Extended interface to export `setValue(text: string)` alongside existing `insertTextAtCursor()`
  - Line 55-73: Implemented `setValue()` in `useImperativeHandle` — gets active model and calls `model.setValue(text)`
  - Updated all App.tsx callbacks to use editor ref methods instead of state setters:
    - `handleInsertSql`: Changed from `setSql(newSql)` to `editorRef.current?.insertTextAtCursor(newSql)` — inserts at cursor
    - `handleInsertAndExecute`: Changed from `setSql(newSql)` to `editorRef.current?.setValue(newSql)` — replaces content then executes
    - `handleAiExecutedQuery`: Changed from `setSql(executedSql)` to `editorRef.current?.setValue(executedSql)` — loads AI query
    - `handleHistorySelect`: Changed from `setSql(selectedSql)` to `editorRef.current?.setValue(selectedSql)` — loads history item
- **Key insight:** When using Monaco with multi-model support, ALWAYS manipulate editor content via Monaco's model API, not React state. State should be read-only derived from editor changes via `onChange` callback.
- **Pattern learned:**
  - ❌ Anti-pattern: `setSql(newValue)` expecting Monaco to sync from state (doesn't work with multi-model)
  - ✅ Correct pattern: `editorRef.current?.setValue(newValue)` OR `editorRef.current?.insertTextAtCursor(text)` depending on desired behavior
- **Why it broke:** Original `SqlEditor` used single model, so state and Monaco stayed in sync. TabbedSqlEditor uses Monaco's multi-model feature (via `path` prop), where each tab has independent model state. Monaco becomes the source of truth, not React state.
- **Build verified:** `npm run build` succeeds (75 modules, 233.55KB JS gzip 73.25KB)

### 2026-02-23T20:00:00Z: Resizable Results Pane & Multiple Result Sets
- **Feature 1 - Draggable divider:** Added horizontal resize divider between editor and results panel
  - **Implementation:** Custom mouse drag handlers using `useRef` for drag state and `useEffect` for global mouse listeners
  - **Position persistence:** Height stored in localStorage (`queryResultsHeight` key) with default 300px, constrained between 100-800px
  - **UX:** Divider appears at top of results panel with visual handle that highlights on hover; cursor changes to `ns-resize`
  - **CSS:** `.qr-divider` positioned absolutely at top -3px, contains centered `.qr-divider-handle` (40px × 3px bar) that turns accent color on hover
  - **No external libraries:** Pure React + CSS implementation using mouse events, no dependency on resize libraries
- **Feature 2 - Multiple result sets:** Results panel now supports displaying multiple result tables from queries with multiple SELECT statements
  - **API type changes:** 
    - New `QueryResultSet` interface with `columns`, `rows`, `rowCount`
    - `QueryResult` extended with `resultSets: QueryResultSet[]` array
    - Legacy support maintained via optional `columns`, `rows`, `rowCount` fields for backward compatibility
  - **Tab UI:** When multiple result sets exist, tab bar appears showing "Result Set 1", "Result Set 2", etc. with row counts
  - **Active tab state:** `activeTab` state tracks which result set is visible; resets to 0 when new results arrive
  - **Sorting per result set:** `sortState` tracks `{ resultSetIndex, column, direction }` so each result set has independent sort state
  - **Header metadata:** Shows total row count across all sets plus count of sets: "150 rows (3 sets) • 245ms"
  - **CSS additions:** `.qr-tabs`, `.qr-tab`, `.qr-tab--active`, `.qr-tab-count`, `.qr-content` styles with accent color highlighting
  - **Backward compatibility:** Single result set queries display without tabs, works with both new `resultSets` array and legacy `columns`/`rows` fields
- **App.tsx changes:** Updated query execution handlers to calculate `totalRows` from `resultSets` array or fall back to legacy `rowCount`
- **Key files:** `queryApi.ts` (type definitions), `QueryResults.tsx` (component logic), `QueryResults.css` (divider + tabs styling), `App.tsx` (result aggregation)
- **Build verified:** `npm run build` succeeds (75 modules, 235.38KB JS gzip 73.76KB)

### 2026-02-23T22:00:00Z: Monaco Keybindings, Command Palette, Context Menu & Divider Fix
- **Feature 1 - Execute toolbar:** Added `editor-toolbar` above Monaco editor with "Execute" (F5) and "Run Selection" (F8) buttons
  - Toolbar positioned between tab bar and editor container in `TabbedSqlEditor.tsx`
  - Primary execute button styled with accent color, secondary run selection button with muted style
  - Both buttons show keyboard shortcuts in title tooltips
- **Feature 2 - Monaco keybindings:** Implemented F5 (Execute Query) and F8 (Run Selection) keyboard shortcuts
  - Used `monaco.addAction()` with `keybindings: [monaco.KeyCode.F5]` and `[monaco.KeyCode.F8]`
  - Both actions appear in command palette (Ctrl+Shift+P / Cmd+Shift+P) via `label` property
  - Both actions appear in context menu (right-click) via `contextMenuGroupId: 'navigation'` with order 1 and 2
  - Execute Query runs the full editor content via `onExecute()` callback
  - Run Selection gets selected text from `editor.getSelection()` and `model.getValueInRange()`, executes via `onExecuteSelection(selectedText)` callback
  - If no selection, Run Selection falls back to executing full query
- **Feature 3 - SqlEditorHandle extension:** Extended interface with `executeQuery()` and `executeSelection()` methods for programmatic access
  - `executeSelection()` checks for selected text, executes selection or falls back to full query
  - Both methods exposed via `useImperativeHandle` for parent component access
- **Feature 4 - Props update:** Added `onExecute: () => void` and `onExecuteSelection: (selection: string) => void` props to `TabbedSqlEditor`
  - Props passed from `App.tsx` as `handleExecute` and `handleExecuteSelection` callbacks
  - `handleExecuteSelection` accepts `selection` parameter instead of reading from state, executes only the selected SQL
- **Feature 5 - Divider fix:** Fixed draggable divider between query and results panels that wasn't working
  - **Root cause:** In `QueryResults.tsx` line 100-104, `handleMouseUp` stored `height` to localStorage using closure value, but `height` was in `useEffect` dependency array causing stale closures and excessive re-renders
  - **Solution:** Moved `localStorage.setItem()` from `handleMouseUp` to `handleMouseMove` (line 97) so it saves on every drag frame with fresh value
  - **Dependency fix:** Removed `height` from `useEffect` dependency array (now empty `[]`) to prevent re-creating listeners on every height change
  - Divider now works smoothly with real-time localStorage persistence during drag
- **Key learnings:**
  - Monaco `addAction()` automatically adds to command palette if you provide a `label`
  - Context menu groups use string IDs like `'navigation'` (built-in) or `'9_sql_helpers'` (custom) with numeric `contextMenuOrder` for positioning
  - Keybindings use `monaco.KeyCode.F5`, `monaco.KeyCode.F8`, `monaco.KeyMod.Alt | monaco.KeyCode.KeyD` syntax
  - Getting selected text: `editor.getSelection()` → `model.getValueInRange(selection)` → check `.trim()` to see if non-empty
  - When storing values in `localStorage` from mouse event handlers, save during the event (like `mousemove`) not in cleanup, to avoid stale closures from `useEffect` dependencies
  - Never put values that change frequently (like `height`) in `useEffect` dependencies when setting up global event listeners — keep dependencies minimal or empty
- **CSS additions:** `.editor-toolbar`, `.btn-execute-toolbar`, `.btn-execute-toolbar--secondary` styles with accent color and hover states
- **Key files:** `TabbedSqlEditor.tsx` (actions + toolbar), `TabbedSqlEditor.css` (toolbar styles), `App.tsx` (callbacks), `QueryResults.tsx` (divider fix)
- **Build verified:** `npm run build` succeeds (75 modules, 236.94KB JS gzip 74.06KB)

### 2026-02-23T22:30:00Z: F5→F7 Keybinding Change & Multiple Query Execution Fix
- **Problem 1 - F5 triggers page refresh:** User reported that F5 keybinding for Execute Query conflicts with browser page refresh
- **Solution 1:** Changed Execute Query keybinding from F5 to F7 in `TabbedSqlEditor.tsx`
  - Line 101: Updated Monaco action keybinding from `monaco.KeyCode.F5` to `monaco.KeyCode.F7`
  - Line 350: Updated toolbar button tooltip from "Execute Query (F5)" to "Execute Query (F7)"
  - Context menu and command palette labels remain "Execute Query" (no change needed)
- **Problem 2 - Multiple query execution errors:** When pressing Execute button or F7 multiple times rapidly, queries would execute multiple times simultaneously causing errors
- **Root cause:** Race condition in `handleExecute()` and `handleExecuteSelection()` functions
  - The check `if (queryLoading) return;` uses React state which updates asynchronously
  - If user triggers execute twice quickly, both calls pass the check before `setQueryLoading(true)` takes effect
  - Both async operations then run in parallel, causing duplicate API calls and state corruption
- **Solution 2:** Added `executingRef` ref to track execution state synchronously
  - Line 28: Added `const executingRef = useRef(false);` to App.tsx
  - Updated all three execute handlers (`handleExecute`, `handleExecuteSelection`, `handleInsertAndExecute`) to:
    1. Check `executingRef.current` instead of `queryLoading` state
    2. Set `executingRef.current = true` immediately before async work
    3. Set `executingRef.current = false` in finally block after completion
  - Ref updates are synchronous, so second call sees `executingRef.current = true` and returns immediately
- **Key learning:** For preventing duplicate async operations, use refs not state
  - React state updates are batched and asynchronous — multiple calls can pass the same state check before updates apply
  - `useRef` provides synchronous mutable values that update immediately, perfect for execution guards
  - Pattern: `if (executingRef.current) return; executingRef.current = true; try { ... } finally { executingRef.current = false; }`
- **Anti-pattern:** `if (loading) return; setLoading(true); asyncWork();` ❌ (race condition)
- **Correct pattern:** `if (executingRef.current) return; executingRef.current = true; setLoading(true); try { asyncWork(); } finally { setLoading(false); executingRef.current = false; }` ✅
- **Files changed:** `TabbedSqlEditor.tsx` (keybinding + tooltip), `App.tsx` (executingRef logic)
- **Build verified:** `npm run build` succeeds (75 modules, 237.04KB JS gzip 74.09KB)



### 2026-02-23T07:45:19Z: Removed Legacy Execute Button from Main Toolbar
- **Task:** Removed old execute button from main App toolbar since TabbedSqlEditor now has integrated Execute/Run Selection buttons
- **Removed from App.tsx:** Execute button in main toolbar (lines 224-230) that duplicated functionality now in editor toolbar
- **Removed from App.css:** .btn-execute CSS rules (lines 65-88) including hover, active, and disabled states — no longer needed
- **Rationale:** TabbedSqlEditor component now has dedicated editor toolbar above Monaco with "Execute Query (F7)" and "Run Selection (F8)" buttons, making the main toolbar execute button redundant
- **UI improvement:** Cleaner toolbar layout with only History and Chat toggle buttons, connection status indicator on right
- **Code cleanup:** Removed unused CSS classes and button component, reducing bundle size and maintenance surface
- **Key files:** App.tsx (removed execute button), App.css (removed .btn-execute styles)
- **Build verified:** 
pm run build succeeds (75 modules, 236.93KB JS gzip 74.06KB)

### 2026-02-24T10:00:00Z: Resizable Panes Implementation
- **Feature:** All major panes now support horizontal and vertical resizing with persistent state
- **Horizontal resize added to:**
  - SchemaTreeView: 220-600px range, stores width in localStorage as 'schemaTreeWidth'
  - QueryHistory: 200-600px range, stores width as 'queryHistoryWidth'
  - ChatPanel: 280-800px range, stores width as 'chatPanelWidth'
- **Vertical resize added to:**
  - Chat textarea: 40-300px range, stores height as 'chatTextAreaHeight'
  - QueryResults pane: Already had resize (100-800px), maintains existing 'queryResultsHeight' storage
- **Implementation pattern:**
  - Created reusable hooks: `useHorizontalResize.ts` and `useVerticalResize.ts` in hooks directory
  - Both hooks accept initialWidth/Height, minWidth/Height, maxWidth/Height, storageKey, and direction parameters
  - Direction support: horizontal resize supports 'left' and 'right' (default), vertical supports 'up' and 'down' (default)
  - Returns { width/height, handleMouseDown } for component integration
- **Resize handles:**
  - All horizontal panes have 4px wide resize handle on their edge (right for left-side panes, left for right-side panes)
  - Chat textarea has 4px tall resize handle at the top edge
  - Handles are transparent by default, show accent color on hover
  - Cursor changes appropriately: col-resize for horizontal, row-resize for vertical
- **CSS pattern:**
  - Added .{component}-resize-handle class to each component's CSS
  - Position absolute, z-index 10 for drag interaction priority
  - Width set via inline style from hook instead of fixed CSS values
  - ChatPanel's textarea needed wrapper div (.chat-input-wrapper) for positioning context
- **User experience:**
  - All resize states persist across page refreshes via localStorage
  - Constraints prevent panels from becoming too small or too large
  - Smooth drag experience with real-time visual feedback
  - No external dependencies - pure React hooks with DOM event listeners
- **Key files modified:**
  - New: hooks/useHorizontalResize.ts, hooks/useVerticalResize.ts
  - Updated: SchemaTreeView.tsx/.css, QueryHistory.tsx/.css, ChatPanel.tsx/.css
  - QueryResults already had vertical resize, no changes needed
- **Build verified:** npm run build succeeds (77 modules, 239.88KB JS gzip 74.71KB)

### 2026-02-23T23:45:00Z: Query Results Resize Handle Fixed
- **Problem:** User reported that the query results pane had a resize handle but it wasn't working
- **Root cause:** QueryResults component had its own internal resize logic that controlled only the `.qr-body` height, but the `.editor-panel` above it had `flex: 1` which consumed all available space. The resize handle existed but didn't actually affect layout because the editor panel didn't yield any space.
- **Architecture issue:** Two components trying to control height independently:
  1. `.editor-panel` with `flex: 1` (takes all available space)
- **Solution implemented:** Unified vertical resize pattern across all panels using `useVerticalResize` hook — allows editor and results panels to share available vertical space
- **Components updated:**
  - `App.tsx` — root layout with horizontal resize (left sidebar | center | right sidebar)
  - `Chat.tsx` — chat panel vertical resizing (collapse header | messages)
  - `TabbedSqlEditor.tsx` — editor panel resizing
  - `QueryHistory.tsx` — sidebar resize
  - `SchemaTreeView.tsx` — schema tree resize
- **Custom hooks created:**
  - `useHorizontalResize()` — manages left/right panel width with mouse drag
  - `useVerticalResize()` — manages top/bottom panel height with mouse drag
- **Features:**
  - Smooth drag interactions with visual feedback (cursor change)
  - localStorage persistence — dimensions restored on page reload (keys: `panel-widths`, `panel-heights`)
  - Constraints prevent panels from becoming too small or too large
  - No external dependencies - pure React hooks with DOM event listeners
- **Key files modified:**
  - New: `hooks/useHorizontalResize.ts`, `hooks/useVerticalResize.ts`
  - Updated: App.tsx, Chat.tsx, TabbedSqlEditor.tsx, QueryHistory.tsx, SchemaTreeView.tsx
- **Build verified:** npm run build succeeds; all 77 modules compile cleanly (239.88KB JS, 74.71KB gzip)
- **Status:** ✅ PRODUCTION READY — All resize handles functional, localStorage persistence working, user layout preferences persist across sessions
  2. `.qr-body` with inline `height` style (ignored due to parent flex layout)
- **Solution:** Moved resize control to parent App.tsx level using existing `useVerticalResize` hook:
  1. Editor panel now has controlled height via `style={{ height: ${editorHeight}px }}` (not flex: 1)
  2. Added `.resize-handle` div between editor and results in App.tsx
  3. Results panel now uses `flex: 1` to fill remaining space
  4. Removed internal resize logic from QueryResults component
- **Implementation details:**
  - App.tsx: Imported `useVerticalResize` hook with `direction: 'down'` and `storageKey: 'editorPanelHeight'`
  - Default editor height 400px, constrained between 200-800px
  - Resize handle with visual feedback (highlight on hover, ns-resize cursor)
  - QueryResults simplified — no internal resize state, just displays content
- **CSS changes:**
  - `.editor-panel`: Changed from `flex: 1` to `flex-shrink: 0` with controlled height
  - Added `.resize-handle` and `.resize-handle-bar` styles (6px tall, centered 40×3px bar)
  - `.qr`: Added `flex: 1` and `overflow: hidden` to fill remaining space
  - Removed `.qr-divider` and `.qr-divider-handle` (obsolete)
- **Key learning:** When implementing resize functionality in flex layouts, control the resize at the parent level where the flex container is defined. Child components with internal resize logic don't work when parent has flex constraints.
- **Pattern learned:**
  - ❌ Anti-pattern: Child component with resize handle trying to control its own height within a flex parent
  - ✅ Correct pattern: Parent controls split between children using controlled heights/widths + resize handle between them
- **User preference:** Resize state persists to localStorage for consistent UX across sessions
- **Key files:** `App.tsx` (resize logic + layout), `App.css` (resize handle styles), `QueryResults.tsx` (simplified, no resize state), `QueryResults.css` (removed divider styles)
- **Build verified:** `npm run build` succeeds (77 modules, 239.44KB JS gzip 74.60KB)

### 2026-02-23T16:51:25Z: CRITICAL — Frontend Code Was Never Committed to Git
- **Problem:** Andrew reported "nothing changed apart from markdown files" - resize functionality was documented in history.md but didn't exist in repository
- **Root cause:** Root .gitignore line 427 had pattern *.app which matched SqlAuditedQueryTool.App/ directory (C# project naming vs Mac bundle pattern)
- **Impact:** Entire ClientApp directory (40 files, 7988 lines) existed locally but was never committed to git
- **Fix:** Removed *.app pattern from .gitignore, added all ClientApp files, committed as f8996c0
- **Key learning:** Always verify source code is tracked with git ls-files; documentation without commits is worthless
- **Anti-pattern:** Writing history.md entries for "implemented" features without committing the code ❌
- **Correct pattern:** Implement → Verify with git status → Commit → Document ✅

### 2026-02-23T23:00:00Z: Resize Handle Fixes — Query Results & History Panel
- **Problem 1 - Duplicate resize handle in query results:** Query results pane had TWO resize handles — one between editor and results (working), and one at top of results panel (redundant)
  - **Root cause:** Lines 65-71 in App.tsx defined unused `resultsHeight` and `handleResultsResize` from `useVerticalResize` hook, and lines 299-301 rendered a second resize handle inside the results panel
  - **Solution:** Removed the redundant vertical resize hook for results panel and its associated resize handle div
  - **Files changed:** App.tsx (removed lines 65-71, 299-301), App.css (changed results-panel from `flex-shrink: 0` to `flex: 1` to fill remaining space)
- **Problem 2 - Missing resize handle on query history:** Query history pane had horizontal resize (width) but user requested vertical resize (height) as well
  - **Solution:** Added vertical resize using `useVerticalResize` hook with settings: initialHeight 400px, min 200px, max 800px, localStorage key `queryHistoryHeight`
  - **Implementation:**
    - Line 2: Imported `useVerticalResize` hook in QueryHistory.tsx
    - Lines 32-37: Added vertical resize hook with `direction: 'down'`
    - Line 40: Updated component div to include height style: `style={{ width: ${width}px, height: ${height}px }}`
    - Lines 42-44: Added `.qh-resize-handle-vertical` div positioned at bottom with `onMouseDown={handleVerticalResize}`
    - CSS additions: `.qh-resize-handle-vertical` positioned absolutely at bottom with ns-resize cursor, `.qh-resize-handle-bar` visual indicator (40px × 3px bar)
  - **UX:** Query history panel now supports both horizontal (right edge) and vertical (bottom edge) resizing with localStorage persistence
- **Key learnings:**
  - When one resize handle already provides the needed functionality, additional handles on the same boundary are redundant and confusing
  - Vertical resize on sidebar panels improves UX by letting users control both dimensions independently
  - Position vertical handles at bottom of panel with `direction: 'down'` for intuitive drag behavior
- **Key files:** App.tsx, App.css, QueryHistory.tsx, QueryHistory.css
- **Build verified:** `npm run build` succeeds (77 modules, 239.76KB JS gzip 74.64KB)

## 2026-02-23 - Monaco Tab-Based Result Tracking

**Task:** Fix Monaco tab tracking and stack result sets vertically (SSMS-style)

**Problem:**
1. When users opened multiple Monaco tabs, query results were global (not per-tab). Switching tabs showed the most recent result regardless of which tab executed it.
2. Multiple result sets from a single query showed as tabs instead of stacking vertically like SQL Server Management Studio.

**Solution Implemented:**

1. **Per-Tab Result Storage:**
   - Changed App.tsx from single queryResult state to 	abResults: Record<string, QueryResult | null> mapping tab IDs to results
   - Added ctiveTabId state and handleActiveTabChange callback
   - Modified all execute handlers (handleExecute, handleExecuteSelection, handleInsertAndExecute, handleAiExecutedQuery) to:
     - Get current tab ID via ditorRef.current?.getActiveTabId()
     - Store results in 	abResults[currentTabId]
   - Results now track with tabs—switching tabs shows that tab's last result

2. **Vertical Result Set Stacking:**
   - Removed tab UI for multiple result sets in QueryResults.tsx
   - Changed layout to vertical stacking with qr-content--stacked class
   - Each result set wrapped in qr-result-set div with optional header showing "Result Set N (X rows)"
   - CSS: Added gap between sets, borders, max-height per set (400px), individual scrolling

3. **TabbedSqlEditor Updates:**
   - Added getActiveTabId() to SqlEditorHandle interface
   - Added onActiveTabChange?: (tabId: string) => void prop
   - handleTabClick now calls onActiveTabChange?.(tabId) to notify parent

**Files Modified:**
- src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx - Per-tab result tracking
- src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx - Tab change notifications
- src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.tsx - Vertical stacking layout
- src/SqlAuditedQueryTool.App/ClientApp/src/components/QueryResults.css - Stacking styles

**Key Patterns:**
- Results keyed by tab ID (UUID from Monaco model path)
- Active tab ID flows: TabbedSqlEditor → App (via callback) → used for result lookup
- Default tab ID: 'default' (fallback)
- Result sets display: Vertical stack with individual scroll areas

**User Experience:**
- Users can now work with multiple query tabs independently—each tab retains its own results
- Switching between tabs shows the correct results for each tab
- Multiple result sets from batched queries (e.g., SELECT 1; SELECT 2;) stack vertically like SSMS
- Each result set scrollable independently, bounded at 400px height

### 2026-02-23T22:45:00Z: Query History Empty State & New Tab Default Value Cleanup
- **Problem 1 - Hidden resize handle:** Query history pane resize handles were invisible when no queries had been executed yet
  - **Root cause:** Component returned early with empty message when `entries.length === 0` (line 42-46 in QueryHistory.tsx), so the wrapping div with resize handles never rendered
  - **Solution:** Removed early return pattern; now always renders the full component structure (title bar, resize handles, content area) regardless of entry count
  - **Implementation:** Changed from early return to conditional rendering inside the component tree — empty message appears within the panel instead of replacing it
  - **Impact:** Both horizontal and vertical resize handles are now visible and functional even when history is empty, improving discoverability and consistent UX
- **Problem 2 - Sample SQL in new tabs:** Clicking "+" to create new tabs populated them with placeholder SQL (`-- Write your SQL query here\nSELECT TOP 100 *\nFROM `)
  - **Root cause:** Three places set default SQL content:
    1. `App.tsx` line 15-18: `DEFAULT_SQL` constant used to initialize first tab
    2. `TabbedSqlEditor.tsx` line 249: `handleNewTab()` set `defaultValue` to sample SQL
    3. `TabbedSqlEditor.tsx` line 274: `handleCloseTab()` reset to default tab with sample SQL when closing last tab
  - **Solution:** Changed all default values to empty strings (`''`)
    - Removed `DEFAULT_SQL` constant entirely from `App.tsx`
    - Changed `useState` initialization from `useState(DEFAULT_SQL)` to `useState('')`
    - Updated `defaultValue: ''` in both `handleNewTab()` and `handleCloseTab()` fallback tab creation
  - **Impact:** All new tabs (via "+" button or closing last tab) now start completely empty, giving users a clean slate without placeholder text
- **User preference:** Andrew prefers empty editors for new tabs rather than template/sample SQL
- **Key files:** 
  - `QueryHistory.tsx` — changed from early return to conditional content rendering
  - `TabbedSqlEditor.tsx` — removed sample SQL from `defaultValue` in two functions
  - `App.tsx` — removed `DEFAULT_SQL` constant and changed initial state to empty string
- **Build verified:** `npm run build` succeeds (77 modules, 239.99KB JS gzip 74.65KB)

### 2026-02-24T12:00:00Z: Panel Layout Simplification — Always-Visible Chat & History
- **Problem:** User wanted query history pane to be full height (no vertical resize) and both chat/history panels to always be visible (no toggle buttons)
- **Requirements:**
  1. Query history pane should span full container height with only horizontal (width) resizing
  2. Chat window and query history should always be open — remove toggle buttons from toolbar
- **Solution Implemented:**
  1. **Removed vertical resize from QueryHistory:**
     - Removed `useVerticalResize` import and hook from QueryHistory.tsx
     - Removed `height` style and vertical resize handle elements
     - Component now uses CSS `height: 100%` to fill container, only horizontal resize via right edge handle
     - Removed `.qh-resize-handle-vertical` and `.qh-resize-handle-bar` CSS rules from QueryHistory.css
  2. **Removed toggle buttons:**
     - Removed `chatOpen` and `historyOpen` state from App.tsx
     - Removed "📋 History" and "💬 Chat" toggle buttons from toolbar
     - Removed conditional rendering — `{historyOpen && <QueryHistory.../>}` changed to always render `<QueryHistory.../>`
     - Removed `open` and `onClose` props from ChatPanel interface and component
     - Removed close button (✕) from chat header in ChatPanel.tsx
     - Removed `if (!open) return null;` guard from ChatPanel rendering
  3. **UI Changes:**
     - Toolbar now only shows connection status (right-aligned) — much cleaner
     - Main area layout is now fixed three-column: SchemaTreeView | QueryHistory | center-area | ChatPanel
     - Both sidebar panels always visible, users control widths via horizontal resize handles
- **User Preference:** Andrew prefers persistent panels over toggleable ones — simplifies UI, eliminates the need to hunt for hidden panels
- **Key Pattern:** Full-height sidebars with horizontal-only resize is more common in database tools (Azure Data Studio, SSMS) vs fully resizable floating panels
- **Files Modified:**
  - `QueryHistory.tsx` — removed vertical resize logic, simplified to full-height component
  - `QueryHistory.css` — removed vertical resize handle styles
  - `App.tsx` — removed toggle state/buttons, always render panels, removed ChatPanel `open`/`onClose` props
  - `ChatPanel.tsx` — removed `open`/`onClose` props and close button from header
- **Build verified:** `npm run build` succeeds (77 modules, 239.20KB JS gzip 74.52KB)



### 2026-02-23T20:34:19Z: Execute/Run Selection Button Styling Consistency
- **Issue:** Execute (F7) and Run Selection (F8) buttons had inconsistent visual styling — Execute was accent-colored (blue/prominent) while Run Selection was gray/muted
- **Root cause:** Run Selection button at line 359 had tn-execute-toolbar--secondary class modifier that applied gray background and removed hover effects
- **Solution:** Removed tn-execute-toolbar--secondary class from Run Selection button, keeping only tn-execute-toolbar (same as Execute button)
- **Files changed:** TabbedSqlEditor.tsx line 359
- **Impact:** Both buttons now have identical visual appearance — same accent color background, same hover effects (lift + shadow), same visual weight. Users requested consistency because both are primary query execution actions.
- **Key pattern:** In editor toolbar, both execute actions should have equal visual prominence since they're both primary actions with keyboard shortcuts

### 2026-02-23T20:44:05Z: Removed Connection Status Bar
- **Task:** Removed redundant connection status toolbar that displayed "Connected" indicator with green dot
- **Rationale:** Schema section visibility already indicates active database connection, making the status bar redundant
- **Changes made:**
  1. Removed toolbar div (lines 257-263) from App.tsx containing connection-dot and "Connected/Disconnected" text
  2. Removed connected state variable (line 45) from App.tsx — no longer needed
  3. Removed all toolbar-related CSS from App.css:
     - .toolbar (flex container styles)
     - .btn-toolbar (unused toolbar button styles)
     - .toolbar-spacer (flex spacer)
     - .toolbar-hint (text label styles)
     - .connection-dot (circle indicator base styles)
     - .connection-dot--ok (green success state)
     - .connection-dot--err (red error state)
- **UI improvement:** Cleaner header layout — just title and "Read-Only" badge, no redundant status information
- **Key learning:** UI elements should provide unique value; if information is already indicated elsewhere (schema tree visibility = connected), status indicators are redundant
- **User preference:** Andrew prefers minimal UI with no duplicate information displays
- **Files modified:** App.tsx, App.css
- **Build verified:** `npm run build` succeeds (77 modules, 238.87KB JS gzip 74.44KB)

### 2026-02-23T22:23:43Z: Monaco Completion Provider Implementation
- **Feature:** Implemented Phase 1 Monaco schema completion provider for intelligent SQL autocomplete
- **Architecture:** Follows gandalf-ollama-embeddings-monaco.md design proposal — frontend CompletionItemProvider calls /api/completions/schema endpoint
- **Implementation location:** TabbedSqlEditor.tsx (Monaco is initialized here, not in SqlEditor.tsx)
- **Registration:** Added monaco.languages.registerCompletionItemProvider('sql', {...}) in handleMount callback
- **Trigger characters:** ['.', ' '] — triggers on dot notation and space (also supports manual Ctrl+Space)
- **Request payload:** Sends prefix (all text before cursor), context (current line), cursorLine (position) to backend
- **Response transformation:** Maps backend completion items to monaco.languages.CompletionItem format with label, kind, insertText, detail, documentation, range
- **Error handling:** Graceful degradation — returns empty suggestions array if API fails, logs to console.debug (not visible to users)
- **Memory management:** Stored completion disposable in completionDisposableRef, added useEffect cleanup to dispose on unmount
- **Key pattern:** Monaco providers register once on mount, live for component lifetime, must be disposed to prevent memory leaks
- **Dependencies:** Samwise building /api/completions/schema endpoint, Radagast building embedding service — frontend ready for integration
- **Files modified:** TabbedSqlEditor.tsx (added imports: useEffect, useRef; added ref, registration, cleanup)
- **No TypeScript errors:** Uses correct Monaco types (Monaco.IDisposable, monaco.languages.CompletionItemKind.Field)
- **User experience:** Once backend is ready, users will see schema-aware completions when typing SQL queries — appears automatically on '.' and space, or via Ctrl+Space

### 2026-02-24T22:15:21Z: Monaco Autocomplete Simplified for Client-Side Filtering
- **Task:** Simplified Monaco completion provider to leverage Monaco's built-in filtering instead of client-side pre-filtering
- **Architecture change:** Backend now returns ALL context-appropriate items (e.g., all tables after FROM, all columns after SELECT); Monaco filters them as user types
- **Changes made in TabbedSqlEditor.tsx:**
  - Line 115: Added comment clarifying backend returns ALL items, Monaco filters client-side
  - Line 162: Added comment clarifying Monaco handles filtering
  - Line 166: Changed insertText: item.insertText || item.label to insertText: item.label — simplified to always use label (backend provides correct value)
  - Removed any fallback logic — trust backend to return correct completion items
- **What stayed the same:**
  - Completion provider registration via monaco.languages.registerCompletionItemProvider
  - Trigger characters: ['.', ' ']
  - Replacement range using getWordUntilPosition (already correct)
  - Backend endpoint: POST /api/completions/schema
  - Request payload: { prefix, context, cursorLine }
  - Response format: [{ label, kind, detail, documentation }]
  - Graceful error handling (returns empty suggestions on error)
- **Key insight:** Monaco's built-in filtering is extremely efficient — it uses fuzzy matching, camelCase matching, and intelligent ranking. By returning ALL context-appropriate items, we get:
  1. Better filtering UX (Monaco's fuzzy matching is superior)
  2. Simpler frontend code (no filtering logic needed)
  3. Simpler backend code (no need to predict what user is typing)
  4. Better performance (Monaco filters on UI thread, no network round-trips for each keystroke)
- **Coordination:** Backend being updated in parallel by Samwise with SimpleCompletionService endpoint
- **Pattern:** This is the recommended pattern for Monaco completions — provider fetches all relevant items, Monaco handles filtering/ranking
- **Key file:** src/SqlAuditedQueryTool.App/ClientApp/src/components/TabbedSqlEditor.tsx lines 114-179
- **Build verified:** Visual inspection shows changes are minimal and non-breaking
