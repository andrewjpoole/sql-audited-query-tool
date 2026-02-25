# Samwise — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly database access only. All queries audited to GitHub issues.
- Owns: DB access layer, API services, audit logging, EF Core discovery

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.Database` — will handle SQL Server readonly access, query execution, EF Core integration
- **Core reference:** All src projects reference Core (no circular deps)
- **App composition:** App references Database along with Audit and Llm
- **Test project:** `SqlAuditedQueryTool.Database.Tests` with xUnit — ready for EF Core and query layer tests
- **Ready to start:** Database layer implementation, connection patterns, EF Core DbContext setup

### 2026-02-22T13:00:00Z: Core Features Implemented
- **Core models:** QueryResult extended with `Rows` property (IReadOnlyList<IReadOnlyDictionary<string, object?>>) for returning actual query data to the API. AuditEntry extended with `GitHubIssueUrl` for audit trail linking.
- **Database layer:** `ReadOnlyConnectionFactory` enforces `ApplicationIntent=ReadOnly` + `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` on every connection. `SqlQueryExecutor` validates queries against 9 forbidden keywords (INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE) via compiled regex before execution.
- **Audit logging:** `GitHubAuditLogger` posts markdown-formatted comments to a configured GitHub issue using Octokit. Includes SHA256 integrity hash, query text in SQL code block, execution stats. Audit failures don't block query results.
- **API endpoints:** `POST /api/query/execute` (execute + audit), `GET /api/query/history` (placeholder). CORS configured for React dev server at localhost:5173.
- **Aspire:** AppHost (`Aspire.AppHost.Sdk/9.2.1`) orchestrates App + SQL Server container. ServiceDefaults added with OpenTelemetry, health checks, service discovery. Both added to solution.
- **Package versions pinned:** EF Core 9.x, Microsoft.Data.SqlClient 6.x (net10.0-only versions excluded). ServiceDefaults OpenTelemetry packages pinned to 1.11.x for net9.0 compat.
- **Merged with Radagast's LLM work:** Program.cs now wires up Database, Audit, and Llm services together with streaming chat, query suggestion, and schema endpoints.

### 2026-02-22: Aspire Ollama Integration via CommunityToolkit
- **AppHost:** Already had `CommunityToolkit.Aspire.Hosting.Ollama` and Ollama resource. Updated `AddModel` call to use clean resource name `"ollamaModel"` with model `"qwen2.5-coder:7b"`.
- **App project:** Added `CommunityToolkit.Aspire.OllamaSharp` package. `Program.cs` calls `builder.AddOllamaApiClient("ollamaModel")` to register `IOllamaApiClient` via Aspire service discovery.
- **Llm project:** Added `OllamaSharp` package. `OllamaLlmService` now takes `IOllamaApiClient` (from OllamaSharp) instead of raw `HttpClient`. Removed manual HTTP/JSON DTOs — uses OllamaSharp typed API.
- **DI wiring:** `LlmServiceCollectionExtensions.AddLlmServices()` no longer uses `AddHttpClient<>`. `IOllamaApiClient` is registered by Aspire; `ILlmService` → `OllamaLlmService` via `AddScoped`.
- **Default model:** `OllamaOptions.Model` default changed from `llama3.2` to `qwen2.5-coder:7b`.
- **Key extension method:** `AddOllamaApiClient` (NOT `AddOllamaSharpApiClient`) from CommunityToolkit.Aspire.OllamaSharp.
- **AppHost path:** `SqlAuditedQueryTool.AppHost\` (at repo root, not under `src\`).

### 2026-02-22: Schema Metadata Enhancement
- **Models enriched:** `SchemaContext.cs` now has `IndexSchema`, `ForeignKeySchema` (new classes), plus `TableSchema` extended with `PrimaryKey`, `Indexes`, `ForeignKeys` lists. `ColumnSchema` extended with `IsPrimaryKey`, `IsIdentity`, `DefaultValue`, `IsComputed`.
- **All new properties use defaults** (empty lists, false, null) — fully backward-compatible with existing LLM and API consumers.
- **Provider upgraded:** `SchemaMetadataProvider` now runs 5 queries total: the original INFORMATION_SCHEMA query for base columns, plus 4 sys.* catalog queries (column extras, primary keys, indexes, foreign keys). Results are merged in-memory via table key lookups.
- **Immutable model update pattern:** Since `ColumnSchema` uses `init` setters, enrichment replaces columns in the list rather than mutating. This is a deliberate pattern.
- **Build note:** Full solution build may fail with file-lock errors if the App is running (PID lock on output DLLs). Individual project builds (`dotnet build src\SqlAuditedQueryTool.Core` etc.) verify compilation cleanly.

### 2026-02-22: Chat API "Failed to fetch" Bug Fix
- **Root cause:** Frontend sends lowercase JSON properties (`messages`, `includeSchema`) but ASP.NET Core's default JSON deserialization is case-sensitive. The `ChatRequest` record expected PascalCase (`Messages`, `IncludeSchema`), causing deserialization to fail silently and return null values, which triggered exceptions downstream.
- **Fix applied:** Added `builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.PropertyNameCaseInsensitive = true; })` in Program.cs to enable case-insensitive JSON property name matching across all API endpoints.
- **Logging enhancement:** Added request logging to `/api/chat` endpoint to track incoming request parameters (SystemPrompt presence, message count, stream flag, includeSchema flag) for easier debugging.
- **Impact:** This fix resolves the "Failed to fetch" error in the chat interface and makes all API endpoints more resilient to casing differences between frontend (camelCase) and backend (PascalCase) conventions.
- **Lesson learned:** ASP.NET Core minimal APIs use System.Text.Json which is case-sensitive by default. When working with JavaScript/TypeScript frontends that use camelCase, always configure `PropertyNameCaseInsensitive = true` for better interoperability.

### 2026-02-22: DI Scoping and Audit Resilience Fixes
- **Problem 1 — Chat endpoint crash:** The `/api/chat` endpoint was calling `app.Services.GetRequiredService<ISchemaProvider>()` which resolves from the root service provider. However, `ISchemaProvider` is registered as **scoped** (per-request lifetime), and scoped services can only be resolved from a scoped provider (like `HttpContext.RequestServices`), not the root singleton provider.
- **Fix 1:** Changed the chat endpoint to inject `ISchemaProvider` as a method parameter instead of manually resolving it. ASP.NET Core's minimal API framework automatically injects scoped services from the request scope: `app.MapPost("/api/chat", async (ChatRequest request, ILlmService llmService, ISchemaProvider schemaProvider, ...) =>`. This follows the framework's DI pattern and avoids scope violations.
- **Problem 2 — Audit logger crashes on startup:** `GitHubAuditLogger` constructor threw `InvalidOperationException` if any of the four required config values (`GitHubAudit:RepoOwner`, `RepoName`, `IssueNumber`, `Token`) were missing. This blocked the entire app from starting in local dev environments where GitHub integration isn't configured yet.
- **Fix 2:** Made `GitHubAuditLogger` gracefully degrade when configuration is missing. Constructor now checks if all four config values are present via `_isConfigured` flag. If missing, it logs a warning and sets `_gitHubClient` to null. The `LogQueryAsync` method checks `_isConfigured` — if true, posts to GitHub; if false, logs locally only and returns an `AuditEntry` with `GitHubIssueUrl = null`. This makes the app usable in dev/test environments without requiring GitHub credentials.
- **Key lesson:** Never resolve scoped services from the root provider — use constructor/method injection or `HttpContext.RequestServices`. For optional external integrations (GitHub, email, etc.), design for graceful degradation: check config availability, log warnings, and provide fallback behavior instead of throwing exceptions on startup.

### 2026-02-22: Query History Tracking for AI-Initiated Queries
- **New models:** Created `QueryHistory` (with `Id`, `Sql`, `RequestedBy`, `Source`, timestamps, execution stats, audit URL) and `QuerySource` enum (User | AI) in Core/Models to track all executed queries with source differentiation.
- **History store:** Added `IQueryHistoryStore` interface with `AddAsync`, `GetAllAsync(limit)`, and `GetByIdAsync(id)` methods. Implemented `InMemoryQueryHistoryStore` using `ConcurrentDictionary` + `ConcurrentQueue` for thread-safe in-memory persistence. Registered as singleton in DI.
- **API changes:** Updated `POST /api/query/execute` to accept optional `Source` field ("AI" for Ollama queries), create QueryHistory entries after audit, and return `historyId` in response. Implemented `GET /api/query/history?limit=N` endpoint to retrieve recent query executions with full metadata.
- **Source tracking:** Queries from Ollama are marked with `RequestedBy = "Ollama"` and `Source = QuerySource.AI`. User queries use `Source = QuerySource.User`. This enables frontend to display query origin and load AI queries into the query window.
- **Security maintained:** All queries (user + AI) still flow through `SqlQueryExecutor` validation (readonly enforcement) and `GitHubAuditLogger` (audit trail). No bypass path exists.
- **Integration ready:** Radagast can now implement Ollama tool calling using `POST /api/query/execute` with `source: "AI"`. The API returns `historyId` for tracking, and the frontend can retrieve history via `GET /api/query/history`.
- **Future work:** Can replace `InMemoryQueryHistoryStore` with DB-backed implementation (EF Core) without changing consumers. Add filtering/pagination to history endpoint as needed.

### 2026-02-22: Chat Response Timeout Configuration
- **Problem identified:** LLM chat responses were timing out at 30 seconds, which is too short for complex queries requiring multiple tool calls or long-running analysis.
- **Default timeout changed:** Increased default chat timeout from 30 seconds to 120 seconds (2 minutes) based on Andrew's request.
- **Configuration added:** Added `ChatTimeoutSeconds` property to `OllamaOptions` configuration class with default value of 120. Created computed property `ChatTimeout` that returns `TimeSpan.FromSeconds(ChatTimeoutSeconds)` for convenient use.
- **HttpClient configuration:** Modified `Program.cs` to configure the HttpClient timeout for the "ollamaModel" named client using `IConfigureOptions<HttpClientFactoryOptions>`. The timeout is now dynamically read from `OllamaOptions.ChatTimeout` at startup.
- **Settings updated:** Added `"ChatTimeoutSeconds": 120` to `appsettings.json` in the Llm section. This makes the timeout easily configurable without code changes - users can adjust it in appsettings.json or appsettings.Development.json.
- **Key files modified:** `src\SqlAuditedQueryTool.Llm\Configuration\OllamaOptions.cs`, `src\SqlAuditedQueryTool.App\Program.cs`, `src\SqlAuditedQueryTool.App\appsettings.json`.
- **Pattern learned:** When configuring named HttpClients registered by Aspire extensions, use `IConfigureOptions<HttpClientFactoryOptions>` with `ConfigureNamedOptions` to apply settings like timeout. The named client must match the registration name ("ollamaModel" in this case).
- **Note:** The timeout applies to the entire HTTP request/response cycle with Ollama. For streaming chat, this is the timeout for establishing the stream, not for receiving chunks. For non-streaming chat with tool calling loops, this is the timeout per individual LLM call.

### 2026-02-23T17:28:00Z: QueryResults Resizing Delegation
- **User request:** Andrew wanted the QueryResults window to be resizable following timeout issue resolution.
- **Delegation:** Routed to software-engineer agent as this is a frontend React change (Legolas owns frontend).
- **Implementation:** Software-engineer added resizing using existing `useVerticalResize` hook pattern (already established by Legolas for other panels).
- **Changes:** Modified `App.tsx` to add second vertical resize hook for results panel (300px initial, 100-600px range, localStorage key `'resultsPanelHeight'`), added resize handle above QueryResults, updated CSS for proper flex/overflow handling.
- **Pattern consistency:** Followed exact same pattern as editor panel resize - visual feedback, localStorage persistence, smooth dragging, directional configuration.
- **Key lesson:** When user requests frontend changes, delegate to software-engineer or check if Legolas pattern already exists. Backend dev shouldn't touch React components directly.

### 2026-02-22: Aspire Frontend Port Configuration
- **Problem:** Frontend ViteApp was running on a random port assigned by Aspire instead of the expected port 5173 configured in vite.config.ts.
- **Root cause:** The AppHost's `AddViteApp` call didn't specify a port, so Aspire allocated one dynamically. Vite's configuration specifies port 5173, but Aspire needs to be told to use that port explicitly.
- **Fix:** Added `.WithHttpEndpoint(port: 5173)` to the frontend ViteApp resource in AppHost.cs. This ensures Aspire assigns the correct port that matches Vite's configuration.
- **Pattern:** For Aspire-managed apps with specific port requirements (like Vite dev servers), always use `.WithHttpEndpoint(port: <desired_port>)` to avoid port conflicts and ensure predictable URLs.
- **File modified:** `SqlAuditedQueryTool.AppHost\AppHost.cs` — frontend resource now explicitly binds to port 5173.

### 2026-02-22: Aspire Duplicate Endpoint Fix
- **Problem:** `DistributedApplicationException: Endpoint with name 'http' already exists` when running AppHost after adding `.WithHttpEndpoint(port: 5173)`.
- **Root cause:** The `.AddViteApp()` method automatically creates a default HTTP endpoint. Calling `.WithHttpEndpoint(port: 5173)` without specifying a name tried to create a second endpoint also named "http", causing a conflict.
- **Fix:** Changed `.WithHttpEndpoint(port: 5173)` to `.WithHttpEndpoint(port: 5173, name: "vite")` to give the endpoint a unique name.
- **Key lesson:** When using `.WithHttpEndpoint()` on Aspire resources that already have a default HTTP endpoint (like ViteApp), always provide a unique `name` parameter to avoid duplicate endpoint name conflicts. Multiple endpoints are allowed, but each must have a distinct name.
- **Pattern:** `.WithHttpEndpoint(port: 5173, name: "vite")` instead of `.WithHttpEndpoint(port: 5173)` for resources with implicit HTTP endpoints.

### 2026-02-22: ASP.NET Core Request Timeout Configuration
- **Problem:** Chat endpoint was timing out at 30 seconds with error "The operation didn't complete within the allowed timeout of '00:00:30'." This is separate from the Ollama HttpClient timeout (120s) - it's the ASP.NET Core server-level request timeout enforced by Kestrel.
- **Root cause:** ASP.NET Core's default request timeout is 30 seconds. Long-running LLM chat operations with multi-step tool calling can easily exceed this, especially when the LLM needs to execute multiple SQL queries and process results.
- **Fix:** Added `AddRequestTimeouts()` middleware configuration in Program.cs to extend the default request timeout from 30 seconds to 5 minutes (300 seconds). This is configured before the app is built and the middleware is activated via `app.UseRequestTimeouts()` in the middleware pipeline.
- **Configuration approach:** Used `Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy` with a `DefaultPolicy` that applies to all endpoints. This is cleaner than per-endpoint attributes for our use case where all chat/query operations benefit from longer timeouts.
- **Timeout layering:** Three timeout layers now work together:
  1. **Ollama HttpClient timeout** (120s): Controls timeout for individual HTTP calls to Ollama API
  2. **ASP.NET Core request timeout** (300s): Controls overall request duration from client to server
  3. **Frontend timeout** (60s, per earlier config): Controls client-side wait time before showing error
- **Key lesson:** ASP.NET Core has a server-level request timeout (default 30s) that's independent of HttpClient timeouts. For long-running operations like LLM tool calling, you must configure both the outbound HttpClient timeout (for calls to external services) AND the inbound request timeout (for the server to process the entire request). The request timeout should be longer than the sum of expected tool call operations.
- **File modified:** `src\SqlAuditedQueryTool.App\Program.cs` - added `AddRequestTimeouts()` configuration with 5-minute default policy.

### 2026-02-22: Frontend Timeout Bottleneck Fix (Third Time's the Charm)
- **Root cause finally identified:** After three attempts to fix timeout issues, discovered the actual bottleneck was the **frontend fetch timeout** set to 60 seconds in `ClientApp/src/api/queryApi.ts`. The backend was configured correctly (ASP.NET Core: 300s, Ollama HttpClient: 120s), but the frontend was aborting requests before the backend had a chance to complete them.
- **The timeout chain before the fix:**
  1. Frontend fetch: 60s ← **THE ACTUAL BOTTLENECK**
  2. Ollama HttpClient: 120s
  3. ASP.NET Core request timeout: 300s
- **The fix:** Increased frontend chat timeout from 60 seconds to 180 seconds (3 minutes) in the `chat()` function's default parameter. This gives the LLM tool-calling loop plenty of time to execute multiple queries and process results without the frontend giving up prematurely.
- **The timeout chain after the fix:**
  1. Frontend fetch: 180s
  2. Ollama HttpClient: 120s (will timeout first if a single LLM call takes too long)
  3. ASP.NET Core request timeout: 300s (safety net for the entire request)
- **Key lesson learned:** When debugging timeout issues, check **ALL layers** of the stack systematically, including the client-side fetch timeout. Don't assume the backend is the problem. The timeout that fires first is the one that matters, and in this case it was the frontend layer that was too aggressive.
- **File modified:** `src\SqlAuditedQueryTool.App\ClientApp\src\api\queryApi.ts` - changed default `timeoutMs` parameter from 60000 to 180000.

### 2026-02-23T19:30:00Z: Ollama Embeddings Integration — Phase 1 Backend
**By:** Samwise (Backend)
**What:** Implemented Phase 1 backend infrastructure for Ollama embeddings integration supporting Monaco SQL autocomplete.
**Changes:**
1. **Aspire AppHost:** Added `nomic-embed-text` embedding model resource as `ollamaEmbed`, referenced by the main app alongside the chat model.
2. **Core model:** Created `CompletionContext` record in `Core/Models/Llm/` accepting `Prefix`, `Context`, and `CursorLine` for editor context.
3. **API endpoint:** Added `POST /api/completions/schema` endpoint accepting `CompletionContext`. Returns empty array initially (placeholder implementation).
**Architecture:** Follows gandalf-ollama-embeddings-monaco.md proposal. Endpoint is ready for Radagast to wire the embedding service implementation.
**Key files:**
- `SqlAuditedQueryTool.AppHost\AppHost.cs` — embedding model registration
- `src\SqlAuditedQueryTool.Core\Models\Llm\CompletionContext.cs` — completion context model
- `src\SqlAuditedQueryTool.App\Program.cs` — completions endpoint
**Next steps:** Radagast implements `IEmbeddingService`, `IVectorStore`, and wires the completion service to populate schema-based autocomplete suggestions.
**Pattern:** Placeholder endpoint allows parallel development — frontend can start integrating Monaco CompletionItemProvider while backend embedding service is being built.

### 2026-02-23: Chat Model Configuration Fix — Multiple Named Ollama Clients (UPDATED)
**Problem 1 (Original):** After adding embeddings (nomic-embed-text), chat endpoint started failing with error: "nomic-embed-text does not support chat". The chat service was using the wrong Ollama model.
**Root cause 1:** When Aspire registers multiple named `IOllamaApiClient` instances via `AddOllamaApiClient("ollamaModel")` and `AddOllamaApiClient("ollamaEmbed")`, using `GetRequiredService<IOllamaApiClient>()` returns the LAST registered client. 
**First fix (FAILED):** Changed to use `IHttpClientFactory.CreateClient("ollamaModel")` and construct `OllamaApiClient` manually. This FAILED because the manually created HttpClient doesn't have the base address configured.
**Problem 2 (HttpClient base address):** When constructing `OllamaApiClient` from `httpClientFactory.CreateClient("ollamaModel")`, the HttpClient doesn't have a base address set, causing error: "HttpClient base address is not set!" at construction time.
**Root cause 2:** `AddOllamaApiClient()` configures the HttpClient with the base address internally, but when you bypass this and manually construct the HttpClient via CreateClient, it doesn't get the Ollama endpoint URL.
**Final fix:** Use `GetServices<IOllamaApiClient>()` to get ALL registered IOllamaApiClient instances, then select by index:
```csharp
builder.Services.AddScoped<IChatClient>(sp =>
{
    var allOllamaClients = sp.GetServices<IOllamaApiClient>().ToList();
    // First registered is ollamaModel (chat), second is ollamaEmbed (embeddings)
    var chatClient = allOllamaClients[0];
    return (IChatClient)chatClient;
});
```
**Key lesson:** When using multiple named Aspire Ollama clients:
- `GetRequiredService<IOllamaApiClient>()` returns the LAST registered client (wrong for multi-model)
- `IHttpClientFactory.CreateClient("<name>")` + manual `OllamaApiClient` construction FAILS because base address isn't set
- `GetServices<IOllamaApiClient>()` returns ALL clients in registration order - select by index to get the specific model
**Files modified:** `src\SqlAuditedQueryTool.App\Program.cs` — IChatClient registration now uses `GetServices<>()` to select first client.
**Pattern:** For multi-model Ollama scenarios with Aspire, use `GetServices<IOllamaApiClient>()` and select by index based on registration order.

### 2026-02-23: Chat Timeout Fix — IChatClient Default 10-Second Timeout
**Problem:** After embeddings integration, chat endpoint started timing out again with error: "The operation didn't complete within the allowed timeout of '00:00:10'." This occurred even though the Ollama HttpClient timeout (120s), ASP.NET Core request timeout (300s), and resilience handler timeout (300s) were all properly configured.
**Root cause:** Microsoft.Extensions.AI's `IChatClient.GetResponseAsync()` and `GetStreamingResponseAsync()` methods use a **default 10-second timeout** when no `ChatOptions` parameter is provided. The service was calling these methods with only `messages` and `cancellationToken`, bypassing the configured HttpClient timeout.
**Fix:** Modified `OllamaLlmService.ChatAsync()` and `StreamChatAsync()` to pass `ChatOptions` with the model ID to all IChatClient calls:
```csharp
var chatOptions = new ChatOptions
{
    ModelId = _options.Model
};
var response = await _client.GetResponseAsync(messages, chatOptions, cancellationToken: cancellationToken);
```
**Impact:** Chat requests now properly respect the HttpClient timeout configuration (120s) instead of failing at the 10-second IChatClient default. The timeout chain is now:
1. **Frontend fetch:** 180s (client-side abort)
2. **IChatClient:** Respects HttpClient timeout (120s per LLM call)
3. **HttpClient:** 120s (configured via OllamaOptions.ChatTimeoutSeconds)
4. **ASP.NET Core request:** 300s (server-side safety net)
5. **Resilience handler:** 300s (total request timeout)
**Key lesson:** When using Microsoft.Extensions.AI's IChatClient abstraction, **always provide ChatOptions** to avoid the 10-second default timeout. The ChatOptions parameter is required to properly configure model selection and inherit the underlying HttpClient timeout settings. Without it, requests will timeout prematurely regardless of HttpClient configuration.
**Files modified:** `src\SqlAuditedQueryTool.Llm\Services\OllamaLlmService.cs` — both ChatAsync and StreamChatAsync methods now pass ChatOptions.
**Pattern:** For all IChatClient.GetResponseAsync/GetStreamingResponseAsync calls: `await _client.GetResponseAsync(messages, chatOptions, cancellationToken)` NOT `await _client.GetResponseAsync(messages, cancellationToken)`.

### 2026-02-24: Chat 404 Error — App Must Run via Aspire AppHost
**Problem:** User reported "Error: Response status code does not indicate success: 404 (Not Found)" when using the chat interface.
**Investigation findings:**
1. Frontend (Vite on port 5173) was running, calling `/api/chat` endpoint
2. Backend App was NOT running on port 5001 (checked via Get-NetTCPConnection)
3. Ollama was NOT running on port 11434 (connection refused)
4. SQL Server WAS running on port 44444 (via Aspire container)
5. The `/api/chat` endpoint IS defined in `Program.cs` line 187
**Root cause:** User is running the app OUTSIDE of Aspire orchestration. The application architecture requires Aspire AppHost to:
- Start the backend App on port 5001
- Start Ollama container with models (qwen2.5-coder:7b and nomic-embed-text)
- Wire service discovery between App and Ollama
- Manage container lifecycle and dependencies
**Architecture constraint:** The app uses Aspire's `AddOllamaApiClient()` which depends on Aspire service discovery to resolve the Ollama endpoint. When the App runs directly (e.g., `dotnet run` in App project), the Ollama client has no valid endpoint and requests fail with 404.
**Proper startup procedure:**
1. Start the AppHost project: `dotnet run --project SqlAuditedQueryTool.AppHost`
2. Aspire dashboard will launch (typically https://localhost:17XXX)
3. AppHost will orchestrate: SQL Server container → Ollama container → App → Frontend
4. All dependencies and service discovery are automatically wired
**Alternative for quick testing (if Aspire is not needed):**
- Run local Ollama: `ollama serve` (starts on port 11434)
- Update App's configuration to point to localhost Ollama
- Run App directly: `dotnet run --project src\SqlAuditedQueryTool.App`
- However, this bypasses Aspire's benefits (container management, telemetry, service discovery)
**Key lesson:** Applications using Aspire resource orchestration (containers, service discovery) MUST be started through the AppHost, not by running individual projects. The AppHost is the composition root. Direct project execution will fail with dependency resolution errors (404, connection refused, missing services).
**Files referenced:**
- `SqlAuditedQueryTool.AppHost\AppHost.cs` — Aspire orchestration configuration
- `src\SqlAuditedQueryTool.App\Program.cs` — App depends on Aspire-registered services
**Pattern:** For Aspire-based solutions, always start via `dotnet run --project <AppHost>`, never via individual project execution.

### 2026-02-24: Simplified Autocomplete — Replaced Ollama Embeddings with Direct Schema Filtering
**Problem:** Autocomplete implementation using Ollama embeddings was overly complex (~270 lines) with semantic search, vector stores, background embedding services, and heavyweight infrastructure for basic SQL completion.
**Solution:** Created `SimpleCompletionService` (85 lines) that:
1. Takes `ISchemaProvider` as dependency (already provides schema metadata)
2. Detects SQL context via regex (AfterFrom → tables, AfterSelect → columns, AfterWhere → columns, etc.)
3. Filters schema items by context (no semantic search needed)
4. Returns ALL matching items — Monaco handles client-side prefix filtering
**What was removed:**
- `EmbeddingCompletionService` (270 lines) — complex semantic search with embedding scoring
- `OllamaEmbeddingService` — HTTP client to Ollama for embeddings
- `InMemoryVectorStore` — vector storage and cosine similarity search
- `SchemaEmbeddingService` — background service pre-embedding schema on startup
- `ollamaEmbed` Ollama model (nomic-embed-text) — no longer needed
- All embedding-related DI registrations
**New implementation:**
- `SimpleCompletionService` — 85 lines total
- Context detection: FROM/JOIN → tables only (no keywords), SELECT → columns + keywords, WHERE/AND/OR → columns only
- All filtering done synchronously in-memory (no async vector lookups)
- Returns full schema items for Monaco's built-in prefix matching
**Tests:** Created `SimpleCompletionServiceTests` with 22 comprehensive tests:
- Context detection (FROM, JOIN, SELECT, WHERE, AND, OR)
- Table filtering (all tables returned in FROM/JOIN contexts)
- Column filtering (all columns returned in SELECT/WHERE contexts)
- Keyword exclusion (FROM/JOIN/WHERE contexts exclude keywords)
- Case insensitivity (detects SQL keywords regardless of case)
- Edge cases (empty prefix, whitespace, general context)
**Files changed:**
- Created: `src\SqlAuditedQueryTool.Llm\Services\SimpleCompletionService.cs`
- Created: `tests\SqlAuditedQueryTool.Llm.Tests\SimpleCompletionServiceTests.cs`
- Deleted: `src\SqlAuditedQueryTool.Llm\Services\EmbeddingCompletionService.cs`
- Deleted: `tests\SqlAuditedQueryTool.Llm.Tests\EmbeddingCompletionContextTests.cs`
- Deleted: `tests\SqlAuditedQueryTool.Llm.Tests\Services.cs`
- Updated: `src\SqlAuditedQueryTool.Llm\LlmServiceCollectionExtensions.cs` — simplified DI registration
- Updated: `src\SqlAuditedQueryTool.App\Program.cs` — removed ollamaEmbed client registration
- Updated: `SqlAuditedQueryTool.AppHost\AppHost.cs` — removed nomic-embed-text model
**Key benefits:**
1. **Simplicity:** 85 lines vs 270+ lines of complexity
2. **No external dependencies:** No Ollama embeddings needed
3. **Fast:** Direct filtering, no async vector lookups
4. **Maintainable:** Context detection logic is clear and testable
5. **Reliable:** No embeddings to pre-compute or cache
**Pattern:** For autocomplete scenarios where context is clear (SQL keywords), direct filtering is superior to semantic search. Use embeddings only when semantic understanding is truly needed.
**Test results:** All 22 tests passed — context detection, filtering, case insensitivity all verified.

### 2026-02-24: Obsolete Vector Store Endpoint Cleanup
**Problem:** After removing embeddings/vectorStore infrastructure in favor of SimpleCompletionService, the `/api/debug/vectorstore` endpoint in Program.cs still referenced `IVectorStore` as a parameter, causing build error: "Body was inferred but the method does not allow inferred body parameters."
**Root cause:** The debug endpoint was leftover from the embeddings implementation and served no purpose after the migration to SimpleCompletionService.
**Fix:** Removed the entire `/api/debug/vectorstore` endpoint (lines 401-425) from Program.cs. This endpoint was for diagnostics only and had no frontend consumers.
**Impact:** App now builds and runs successfully. No breaking changes to any functional endpoints.
**Key lesson:** When removing major features (like embeddings infrastructure), search for all dependent code including debug/diagnostic endpoints. Use grep to find references before removing interfaces/services.
**Files modified:** `src\SqlAuditedQueryTool.App\Program.cs` — removed obsolete vectorstore endpoint.


### 2026-02-24: Chat API Response Format Mismatch Fix
**Problem:** Frontend calling /api/chat received response but was expecting different structure. Frontend expects xecutedQuery (string) and xecutedResult (QueryResult object) for AI-executed queries, but backend was only returning xecutedQueries (array) without the result data.
**Root cause:** The backend's /api/chat endpoint was adding executed queries to the response but:
1. Only included metadata (historyId, sql, rowCount, executionTimeMs, auditUrl) without actual row data
2. Didn't include backward-compatible xecutedQuery and xecutedResult fields that the frontend expects
**Fix applied:**
1. Enhanced the xecutedQueries array items to include a esult object with full query results:
   - esultSets array with columns, rows, and rowCount
   - xecutionTimeMs for each query
2. Added frontend compatibility fields to the final response:
   - xecutedQuery = first executed query SQL (string or null)
   - xecutedResult = first executed query result (full QueryResult structure or null)
**Files modified:**
- src\SqlAuditedQueryTool.App\Program.cs — line 286-303 (added result data to executedQueries), line 335-347 (added backward-compatible fields)
**Pattern:** When frontend expects specific response shape (executedQuery/executedResult), ensure backend provides both the modern structure (executedQueries array) and backward-compatible fields. This allows frontend to work correctly whether one or multiple queries were executed.
**Key learning:** API response format mismatches don't always cause 404s or crashes - they can cause silent failures where the endpoint succeeds but the frontend can't properly handle the response structure. Always verify frontend expectations match backend response shape.
