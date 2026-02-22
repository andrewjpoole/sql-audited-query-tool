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

