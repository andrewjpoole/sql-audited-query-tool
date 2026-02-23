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
