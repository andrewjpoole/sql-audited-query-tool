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


