# Decisions

<!-- Append-only. Scribe merges from decisions/inbox/. -->

### 2026-02-22T12:07:00Z: Team formed
**By:** Squad (Coordinator)
**What:** Team hired for SQL Audited Query Tool project. Members: Gandalf (Lead), Samwise (Backend), Radagast (LLM Engineer), Legolas (Frontend), Faramir (Security).
**Why:** Project kickoff — Andrew's initial request.

### 2026-02-22T12:18:00Z: Project Structure — Gandalf
**By:** Gandalf (Lead)
**What:** Established solution structure with 5 src projects and 4 test projects targeting net9.0.
**Layout:**
- `SqlAuditedQueryTool.Core` — domain models, interfaces, shared types (referenced by all)
- `SqlAuditedQueryTool.Database` — SQL Server readonly access, query execution, EF Core
- `SqlAuditedQueryTool.Audit` — GitHub issue audit logging
- `SqlAuditedQueryTool.Llm` — local LLM integration, SQL Server MCP client
- `SqlAuditedQueryTool.App` — ASP.NET Core web app (chat UI host)
- 4 xUnit test projects mirror the src layer
**Why:** Clean separation of concerns — each domain responsibility is isolated, testable, and independently deployable. Core sits at the center with no outward dependencies. App is the composition root.
**Constraints:**
- net9.0 target framework (LTS)
- ASP.NET Core Empty template for App (minimal, no MVC scaffolding — will build chat UI on top)
- xUnit as test framework

### 2026-02-22T14:04:00Z: User Directive — Aspire
**By:** Andrew
**What:** Add .NET Aspire for local testing and development orchestration.
**Why:** Local dev environment management.

### 2026-02-22T14:15:00Z: React Frontend Architecture — Legolas
**By:** Legolas (Frontend Dev)
**What:** Created React SPA inside `ClientApp/` using Vite 7 + React 19 + TypeScript + Monaco SQL editor.
**Key decisions:**
- `@monaco-editor/react` v4.7.0+ for SQL editing with dark theme
- 6 custom context menu commands in "SQL Helpers" group (Insert Date, GUID, GETDATE(), NEWID(), Wrap in SELECT, Toggle Comment)
- Plain CSS with custom properties (no CSS framework)
- SPA served via `Microsoft.AspNetCore.SpaServices.Extensions` v9.x
- Vite dev server on port 5173 proxies `/api` calls to .NET on port 5001
- `GET /api/health` endpoint for connectivity testing
**Why:** Monaco provides professional code editing. Vite ensures fast iteration. SPA middleware keeps frontend and backend deployable together.
**Constraints:**
- SPA package pinned to 9.x for net9.0 compatibility
- Monaco `addAction` callback type is `ICodeEditor`

### 2026-02-22T18:00:00Z: Schema TreeView — Frontend API Contract
**By:** Legolas (Frontend)
**What:** Updated TypeScript types for richer schema data from `GET /api/schema`:
- `SchemaTable.primaryKey: string[]`
- `SchemaTable.indexes: SchemaIndex[]` — with `name`, `columns`, `isUnique`, `isClustered`
- `SchemaTable.foreignKeys: SchemaForeignKey[]` — with `name`, `columns`, `referencedSchema`, `referencedTable`, `referencedColumns`
- `SchemaColumn` fields: `isPrimaryKey`, `isIdentity`, `defaultValue`, `isComputed`
**Why:** Schema treeview needs metadata to display keys, indexes, FK relationships. Also: `SqlEditor` exposes `SqlEditorHandle` via ref for text insertion.

### 2026-02-22T16:00:00Z: Read vs Fix Query UI Separation — Legolas
**By:** Legolas (Frontend)
**What:** Read queries show green "Insert & Execute" button (auto-runs). Fix queries show yellow warning "⚠️ FIX QUERY — Must be run separately" with "Insert into Editor" only (no execute). Determined by `isFixQuery` boolean.
**Why:** Readonly database access — fix queries must never execute. Visual distinction prevents accidental write operations.

### 2026-02-22T20:30:00Z: Schema TreeView Context Menu Pattern — Legolas
**By:** Legolas (Frontend)
**What:** Right-click context menu on tables: SELECT TOP 1000, SELECT COUNT(*), SELECT WHERE.
**Why:** UX improvement — right-click is natural for power users wanting quick templates.

### 2026-02-22T13:00:00Z: Core Features & Aspire — Samwise
**By:** Samwise (Backend)
**What:** Implemented core features:
1. **QueryResult carries row data** — Extended with `Rows` to return actual query results
2. **Write blocklist uses compiled regex** — 9 keywords (INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE) with word-boundary matching
3. **Audit failure non-blocking** — Query still returns if GitHub API fails
4. **Connection isolation** — `ApplicationIntent=ReadOnly` + `READ UNCOMMITTED` isolation
5. **Aspire SDK 9.2.1** pinned to 9.x for net9.0
6. **EF Core 9.x** pinned to prevent net10.0 upgrade
7. **CORS** — Default policy allows `http://localhost:5173`
**Why:** Foundational backend capabilities before frontend can query.

### 2026-02-22T14:20:00Z: Security Contracts for Query Pipeline — Faramir
**By:** Faramir (Security)
**What:** Established three security enforcement contracts:
1. **SqlValidator** — All SQL must pass `ValidateReadOnly()`. `RiskLevel.Blocked` = rejected.
2. **DataLeakPrevention** — All LLM payloads must pass `ValidateLlmPayload()`. Schema only — no row data.
3. **AuditIntegrity** — Every audit entry includes integrity hash via `GenerateAuditHash()`.
**Contracts:**
- **Samwise:** Call `SqlValidator.ValidateReadOnly()` before execution, `SanitizeForAudit()` before logging
- **Radagast:** Call `DataLeakPrevention.ValidateLlmPayload()` on LLM payloads
- **Legolas:** Display `ValidationResult.Violations` when queries rejected
**Blocked keywords:** INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE, GRANT, REVOKE, DENY, sp_*, xp_*

### 2026-02-22T22:00:00Z: Chat Timeout & SQL Block Detection — Legolas
**By:** Legolas (Frontend)
**What:** Implemented timeout handling and SQL block detection:
- Configurable timeout (60s default) via `AbortController`
- Timeout error display: "Request timed out..."
- Chat messages parse markdown SQL code blocks (```sql...```)
- SQL blocks render as action cards with "📝 Insert into Editor" button
**Why:** Clear feedback on timeouts, actionable SQL suggestions from LLM.

### 2026-02-22T22:00:00Z: Chat Timeout Configuration — Samwise
**By:** Samwise (Backend)
**What:** Increased LLM chat response timeout from 30s to 120s, made configurable via `appsettings.json`.
**Implementation:**
- `OllamaOptions.ChatTimeoutSeconds` property (default: 120)
- Modified `Program.cs` to configure Ollama HttpClient timeout
- Added to `appsettings.json` under `Llm` section
**Why:** Complex queries with multiple tool calls exceeded 30s.

### 2026-02-22T23:30:00Z: AI Query Execution & Chat History Persistence — Legolas
**By:** Legolas (Frontend)
**What:** Implemented two features:
1. **AI-Executed Query Display** — Queries run by Ollama auto-load into Monaco. Visual: 🤖 for AI, 👤 for user.
2. **Persistent Chat History** — Sessions persist in localStorage with full message history, timestamps, previews.
**Why:** Transparency on AI execution, ability to resume investigation sessions across restarts.

### 2026-02-23T10:00:00Z: Tabbed Query Editor Implementation — Legolas
**By:** Legolas (Frontend)
**What:** Implemented tabbed SQL editor using Monaco's native multi-model support via `path` prop.
**Features:**
- Create/close/rename tabs
- Dirty state tracking (● indicator)
- Tab bar UI (custom), Monaco handles state
- Unique paths (query-1.sql, query-2.sql) for model management
**Why:** Monaco's multi-model API is battle-tested (VS Code pattern), no manual model lifecycle needed.

### 2026-02-22T21:45:33Z: User Directive — Local LLM Data Access Allowed
**By:** Andrew
**What:** "Strictly without exposing any data" requirement does NOT apply to locally running Ollama. Local LLMs can access database data (runs on user's infrastructure, data never leaves).
**Why:** Clarification — constraint was about external service exposure, not local processing.
**Impact:** Ollama can receive query results, row data, schema. SQL Server MCP integration becomes viable. GitHub audit still captures all queries.

### 2026-02-22T18:00:00Z: LLM Integration Architecture — Radagast
**By:** Radagast (LLM Engineer)
**What:** Established LLM integration patterns:
1. **LLM interfaces in `Core/Interfaces/Llm/`** — separated from DB interfaces
2. **IConnectionFactory in `Core/Interfaces/`** — shared interface for DB connections
3. **Ollama as local backend** — HTTP client to `localhost:11434`, model configurable (default: `llama3.2`)
4. **Streaming via SSE** — `POST /api/chat` with `stream: true`
5. **Query classification** — SQL parsed from markdown, classified read-only or fix query
6. **Schema safety** — SchemaMetadataProvider queries ONLY `INFORMATION_SCHEMA` and `sys.*`
7. **DI lifetimes** — OllamaLlmService scoped, QueryAssistant scoped, MemoryCache singleton
8. **Schema caching** — 5-minute default, configurable
**Impact:** **Samwise** implement `IConnectionFactory`, **Legolas** call `/api/chat` with `stream: true`, **Faramir** review safety boundary.

### 2026-02-22T21:00:00Z: Aspire Ollama Integration — Samwise
**By:** Samwise (Backend)
**What:** Wired Ollama into Aspire using CommunityToolkit packages. `OllamaLlmService` uses `IOllamaApiClient` via Aspire discovery (resource name: `ollamaModel`).
**Changes:**
- AppHost resource name: `ollamaModel`
- Client registration: `builder.AddOllamaApiClient("ollamaModel")`
- LLM DI: Changed to `AddScoped<ILlmService, OllamaLlmService>`
- Default model: `qwen2.5-coder:7b`
**Why:** Aspire discovery replaces hardcoded URLs, enables container orchestration.

### 2026-02-22T21:00:00Z: Migrated LLM Layer to Microsoft.Extensions.AI — Radagast
**By:** Radagast (LLM Engineer)
**What:** Replaced direct OllamaSharp with `Microsoft.Extensions.AI` abstractions (`IChatClient`).
**Changes:**
- `OllamaLlmService` depends on `IChatClient` (not `IOllamaApiClient`)
- Uses `GetResponseAsync()` / `GetStreamingResponseAsync()`
- Llm project no longer has OllamaSharp dependency — provider-agnostic
**Why:** `IChatClient` is standard .NET abstraction. Easy to swap Ollama for OpenAI, Anthropic, etc. without touching Llm layer.

### 2026-02-22T15:45:00Z: SQL Server MCP Security Assessment — Faramir
**By:** Faramir (Security)
**What:** Risk assessment: 🔴 **CRITICAL — REJECT direct MCP integration as originally proposed.**
**Reason:** Violates "strictly without exposing any data" by giving LLM direct database access (prompt injection attack vector, no audit trail, removes 5 of 6 security controls).
**Recommendation:**
- ✅ **APPROVE** current SchemaMetadataProvider architecture (all 6 defenses intact)
- 🟡 **CONDITIONAL APPROVE** schema-only MCP server (custom fork, 2-3 weeks engineering, must harden audit + payload validation)
**Security Contracts:** Readonly enforcement, data leak prevention, audit integrity, schema-only queries, readonly connections, GitHub logging MUST be preserved.

### 2026-02-22T22:00:00Z: MCP Feasibility — Radagast (UPDATED)
**By:** Radagast (LLM Engineer)
**What:** Research found Azure Data API Builder MCP Server v1.7+ exists as **official SQL Server MCP server** with production-ready support.
**Features:**
- Built into DAB v1.7+
- Supports SQL Server, PostgreSQL, MySQL, Azure Cosmos DB
- Role-Based Access Control (RBAC) built-in
- Caching, telemetry, OpenTelemetry
- Native .NET Aspire integration
- Schema-only mode: can disable data tools, expose only `describe_entities` (schema discovery)
**Recommendation:** Implement Azure DAB MCP with schema-only tool configuration (disable `create_record`, `update_record`, `delete_record`, `execute_entity`).

### 2026-02-22T12:30:00Z: Architectural Decision — SQL Server MCP Integration (REVISED) — Gandalf
**By:** Gandalf (Lead)
**What:** **REVISED based on Andrew's clarification**: Recommend **Option B: App-Orchestrated Tool Calling** instead of MCP bridge.
**Architecture:**
```
User ↔ Chat UI ↔ App
                ↓
        OllamaLlmService
                ↓ (function calling)
    App detects tool requests
                ↓
    App calls QueryExecutor
                ↓
    AuditService (GitHub)
```
**Rationale:**
1. Ollama gets data access (improved assistance quality)
2. All queries flow through app (complete audit trail)
3. We control execution path (readonly enforcement)
4. No external MCP bridge process needed
5. Aspire orchestration handles Ollama
**What Changes:**
- Schema context: Tool-based discovery + prompt
- Query execution: LLM can request (app executes)
- Data exposure: LLM can see query results
- Audit trail: All queries still logged ✅
- Readonly enforcement: App-level (unchanged) ✅

### 2026-02-22T12:30:00Z: Tool Calling Infrastructure for Ollama — Radagast
**By:** Radagast (LLM Engineer)
**Status:** IMPLEMENTED (Phase 1 — Infrastructure Ready, Phase 2 blocked on Ollama support)
**What:** Implemented app-orchestrated tool calling infrastructure.
**Models Created:**
- `ToolDefinition`, `ToolCallRequest`, `ToolCallResult`
- `LlmResponse.ToolCalls` — LLM can request tool execution
- `ChatSession`, `ChatMessageHistory`
- `IChatHistoryStore` with `InMemoryChatHistoryStore`
**Chat Endpoint (`/api/chat`):**
- Accept optional sessionId, systemPrompt, messages, includeSchema
- Return sessionId, message, executedQueries[], suggestion
**Tool Calling Loop:**
1. User sends message → saved to history
2. LLM responds (may include tool calls)
3. If tool call: execute query, audit, save to history, feed result back to LLM
4. Continue conversation; final response saved
**Chat History API:**
- `GET /api/chat/sessions` — list sessions
- `GET /api/chat/sessions/{id}` — get session with messages
- `DELETE /api/chat/sessions/{id}` — delete session
**Technical Challenge:** Ollama tool calling support via Microsoft.Extensions.AI incomplete (Ollama response structure differs). Infrastructure built but disabled until Ollama improves.
**What Works:** Chat history (multi-turn persistent), manual query execution (queries saved with Source=User), Ollama integration (chat with schema context).
**Blocked:** Tool calling (awaiting Ollama update or OpenAI/Anthropic switch).

### 2026-02-22T12:00:00Z: Query History Tracking for AI-Initiated Queries — Samwise
**By:** Samwise (Backend)
**What:** Implemented QueryHistory tracking ensuring AI queries (Ollama tool calling) flow through history/audit pipeline.
**Models:**
- `QueryHistory` — comprehensive tracking with Id, Source (User|AI), RequestedBy, execution metadata, GitHubIssueUrl
- `QuerySource` enum — User or AI
**Infrastructure:**
- `IQueryHistoryStore` interface — AddAsync, GetAllAsync(limit), GetByIdAsync(id)
- `InMemoryQueryHistoryStore` — thread-safe in-memory with ConcurrentDictionary
**API Changes:**
- `POST /api/query/execute` — optional `Source` field ("AI" for Ollama, null for user), returns `historyId`
- `GET /api/query/history?limit=50` — real implementation, returns metadata, source, timestamp, results, audit URL
**Integration for Radagast:** Tool call request includes `"source": "AI"`, response includes historyId for frontend tracking.
**Future:** Persistent storage (replace InMemoryQueryHistoryStore with EF Core/SQL), filtering, pagination, retention policy.

### 2026-02-22T12:00:00Z: Timeout Architecture for LLM Chat Operations — Samwise
**By:** Samwise (Backend)
**What:** Established three-layer timeout architecture (shortest to longest):
1. **Ollama HttpClient: 120 seconds** — individual HTTP calls to Ollama
2. **Frontend fetch: 180 seconds** — client-side wait before abort
3. **ASP.NET Core request: 300 seconds** — server-level entire lifecycle
**Rationale:**
- Ollama timeout (120s) fires first if LLM is slow
- Frontend timeout (180s) gives tool-calling loop time
- ASP.NET timeout (300s) is safety net
**Root Cause of Previous Failures:** First two fixes missed frontend fetch timeout at 60s, which was aborting before backend finished.
**Key Lesson:** Check entire timeout chain from client to server; first timeout to fire is the critical one.

### 2026-02-23T15:00:00Z: Aspire Resilience Configuration Order Fix — Gandalf
**By:** Gandalf (Lead)
**What:** Fixed persistent 30-second timeout on `/api/chat` caused by Aspire's `AddStandardResilienceHandler()` applying Polly timeout.
**Root Cause:**
1. `AddServiceDefaults()` calls `ConfigureHttpClientDefaults` adding resilience with 30s timeout to ALL HttpClients
2. Options must be configured BEFORE `AddServiceDefaults()`, not after
3. Named client configuration doesn't work when applied via `ConfigureHttpClientDefaults`
4. Duplicate `ConfigureHttpClientDefaults` calls caused conflicts
**Solution:** Configure `HttpStandardResilienceOptions` globally BEFORE `AddServiceDefaults()`:
```csharp
builder.Services.Configure<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});
builder.AddServiceDefaults();
```
**Key Learnings:**
- Order of operations matters with Aspire ServiceDefaults
- Configure options BEFORE `AddServiceDefaults()`
- Don't call `ConfigureHttpClientDefaults` twice
- Global config affects ALL HttpClients
**Impact:** All HttpClients now have 5-minute timeout (acceptable for LLM operations).

### 2026-02-22T22:00:00Z: DI Scoping and Graceful Degradation — Samwise
**By:** Samwise (Backend)
**What:** Fixed two critical blocking issues:
1. **Chat endpoint DI scope violation:** Changed `/api/chat` to inject `ISchemaProvider` as method parameter (scoped services resolved from scoped provider, not root)
2. **Audit logger startup failure:** Made `GitHubAuditLogger` resilient to missing config (logs warning, operates in local-only mode instead of throwing)
**Why:**
- Scoped services have per-request lifetime, resolved from request scope not root provider
- External integrations (GitHub) shouldn't block app startup; graceful degradation is better than crashes
**Pattern:** DI in minimal APIs use method parameter injection; optional services check config availability and provide fallback behavior.
**Impact:** App starts successfully in local dev, chat endpoint no longer crashes.

### 2026-02-22T12:00:00Z: Chat API JSON Case Sensitivity Fix — Samwise
**By:** Samwise (Backend)
**What:** Configured ASP.NET Core JSON serialization case-insensitive for all API endpoints.
**Context:** Frontend sends camelCase (`messages`, `includeSchema`), backend expects PascalCase (`Messages`, `IncludeSchema`). Default System.Text.Json is case-sensitive → silent deserialization failure → null values → exceptions.
**Implementation:** Added to `Program.cs`:
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```
**Also:** Added request logging to `/api/chat` for debugging.
**Impact:** Resolves "Failed to fetch" error, makes all endpoints resilient to casing differences.

### 2026-02-23T15:00:00Z: Multi-Result Set Support — Gandalf
**By:** Gandalf (Lead)
**What:** Implemented full multi-result set support for queries returning multiple sets.
**Implementation:**
1. Created `QueryResultSet` class
2. Restructured `QueryResult` with `ResultSets` collection
3. Added legacy computed properties for backward compatibility
4. Modified `SqlQueryExecutor` to loop through all result sets via `reader.NextResultAsync()`
5. Added comprehensive logging at executor, API, and frontend levels
**Logging Strategy:**
- Backend per result set: row/column counts
- Backend summary: total sets, total rows, execution time
- Frontend: console logging of result sets received
**Why:** Multi-query support critical for investigation workflows. Logging provides visibility. Backward compatibility maintained.

### 2026-02-22T16:00:00Z: Resilience Handler Timeout Configuration — Gandalf
**By:** Gandalf (Lead)
**What:** Configured `HttpStandardResilienceOptions` for named HttpClient ("ollamaModel"):
```csharp
builder.Services.Configure<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>("ollamaModel", options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});
```
**Root Cause:** Aspire's `AddStandardResilienceHandler()` applies Polly resilience with 30s timeout (overrides `HttpClient.Timeout`).
**Key Learning:** Aspire resilience handler timeout takes precedence; must explicitly configure for long-running ops.
**Layers of Timeout (in order):**
1. HttpClient.Timeout (2 min) — transport
2. Resilience handler (5 min) — **was bottleneck** (30s default)
3. ASP.NET request (5 min) — server
4. Frontend fetch (180s) — client
**Impact:** Chat can handle multi-step tool calling without premature timeout.

### 2026-02-23T15:05:27Z: Resizable UI Panes Implementation — Legolas
**By:** Legolas (Frontend Dev)
**What:** Implemented responsive pane resizing across all UI panels with localStorage persistence.
**Components Updated:**
- App.tsx — root layout resizing
- Chat.tsx — chat panel vertical resizing
- TabbedSqlEditor.tsx — editor panel resizing
- QueryHistory.tsx — history sidebar resizing
- SchemaTreeView.tsx — schema tree resizing
**Hooks Created:**
- `useHorizontalResize` — manages left/right panel width dragging
- `useVerticalResize` — manages top/bottom panel height dragging
**Features:**
- Smooth drag interactions, visual feedback during resize
- localStorage persistence — dimensions restored on page reload
- Prevents accidental over-resize
**Why:** Flexible layout control, user preferences persist across sessions.
**Impact:** Users can customize UI layout; preferences persist via localStorage.

### 2026-02-23T16:00:00Z: Ollama Timeout Fix (5th Attempt) — ConfigureAll Pattern — Gandalf
**By:** Gandalf (Lead)
**What:** Fixed persistent 30-second timeout on `/api/chat` (final resolution after 4 failed attempts). Root cause was `Configure<>` only configures default (unnamed) instance.
**Root Cause (Corrected):**
1. `AddServiceDefaults()` calls `ConfigureHttpClientDefaults` which registers a lambda
2. That lambda executes LATER when HttpClients are created (not when AddServiceDefaults is called)
3. `Configure<T>()` only configures default/unnamed instance
4. Each HttpClient gets its own resilience handler instance
5. Named client configuration doesn't work for options applied via `ConfigureHttpClientDefaults`
**Solution:** Use `ConfigureAll<>` instead of `Configure<>` to apply configuration to ALL resilience handler instances:
```csharp
builder.AddServiceDefaults();

builder.Services.ConfigureAll<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});

builder.AddOllamaApiClient("ollamaModel");
```
**Key Learning:**
- `Configure<T>()` — only configures default/unnamed instance
- `Configure<T>("name", ...)` — only configures specific named instance
- `ConfigureAll<T>()` — configures ALL instances (current and future)
- When using Aspire's `AddStandardResilienceHandler()` which creates per-client instances, you MUST use `ConfigureAll<>`
**Configuration Order:**
1. `AddServiceDefaults()` — registers ConfigureHttpClientDefaults lambda
2. `ConfigureAll<HttpStandardResilienceOptions>()` — sets up global timeout config
3. `AddOllamaApiClient("ollamaModel")` — registers named client
4. HttpClient creation triggers lambda → applies ConfigureAll config
**Impact:** All HttpClients (including Ollama) now respect 5-minute timeout. No more 30-second premature timeouts on long-running LLM operations. `/api/chat` tool calling loop fully functional.
