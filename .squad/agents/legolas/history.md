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
