# Gandalf — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly database access only. All queries audited to GitHub issues.
- Local LLM must never be exposed to actual database data.
- Fix queries are suggested but run in a separate tool.

## Learnings
<!-- Append new learnings below this line -->

### 2025-07-24: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root
- **Target Framework:** net9.0 (using .NET 10 SDK)
- **App type:** ASP.NET Core Empty (`dotnet new web`) for SqlAuditedQueryTool.App — chosen to support chat UI
- **Layered architecture:** Core → Database/Audit/Llm → App (dependency flows inward)
- **Reference graph:**
  - Core is referenced by Database, Audit, Llm, and App
  - App references all four src projects
  - Each test project references its corresponding src project
- **Test framework:** xUnit (default `dotnet new xunit`)
- **Key paths:**
  - Solution: `SqlAuditedQueryTool.sln`
  - Source: `src/SqlAuditedQueryTool.{Core,Database,Audit,Llm,App}/`
  - Tests: `tests/SqlAuditedQueryTool.{Core,Database,Audit,Llm}.Tests/`
- **Andrew preference:** .NET 9.0 LTS target

### 2026-02-22: SQL Server MCP Architecture Decision
- **Question:** Should we use SQL Server MCP to give Ollama direct database access?
- **Decision:** **NO — keep current architecture**
- **Rationale:**
  - Core requirement: *"strictly without exposing any data from the database"*
  - Current design enforces data isolation at architectural level (LLM has no DB connection)
  - SchemaMetadataProvider already provides schema context to LLM
  - MCP would add complexity without functional benefit for our use case
  - Ollama doesn't natively support MCP tool calling — would need adapter
- **MCP Research:**
  - C# MCP server (`aadversteeg/mssqlclient-mcp-server`) has query execution disabled by default
  - Python MCP server (`RichardHan/mssql_mcp_server`) always enables data access — NOT safe for our use case
  - Even schema-only MCP adds attack surface without adding value over SchemaMetadataProvider
- **Architecture Preserved:**
  - `SchemaMetadataProvider` → schema context → Ollama (no data exposure)
  - User executes queries via `QueryExecutor` (readonly)
  - All queries audited to GitHub issues via `AuditService`
- **Security Principle:** Air gap between LLM and database — data leakage is architecturally impossible

### 2026-02-22: MCP Decision REVISED — Local LLM Can Access Data
- **Trigger:** Andrew clarified: *"the 'strictly without exposing any data' requirement doesn't apply to locally running ollama models"*
- **Key Insight:** The original security constraint was about preventing data leakage to **external services**, not local processing. Since Ollama runs locally, data never leaves the infrastructure.
- **New Decision:** **YES — Implement App-Orchestrated Tool Calling**
- **Architecture Change:**
  - Add Ollama tool calling (execute_query, get_schema)
  - Our app orchestrates MCP-style tool calls via existing QueryExecutor
  - Results fed back to Ollama for analysis
  - All queries still flow through AuditService → GitHub issues
- **Why Not External MCP Bridge:**
  - External bridge might bypass our audit trail
  - App-orchestrated approach gives us full control
  - Simpler deployment (no Node.js dependency)
- **Benefits Unlocked:**
  - Ollama can see actual query results
  - Iterative investigation (query → analyze → refine)
  - Much better incident investigation assistance
- **Security Preserved:**
  - Readonly enforcement via QueryExecutor
  - Complete audit trail to GitHub issues
  - Data stays local (Ollama runs on-prem)

### 2026-02-22: Chat Timeout Root Cause Fixed — Resilience Handler
- **Problem:** After three attempts to fix timeouts (Ollama HttpClient, ASP.NET request timeout, frontend fetch), chat was still timing out at exactly 30 seconds with .NET TimeSpan error "00:00:30"
- **Root Cause:** `AddStandardResilienceHandler()` in ServiceDefaults applies a Polly resilience pipeline with a **30-second total request timeout** to ALL HttpClients by default
- **Previous Failed Fixes:**
  1. Set Ollama HttpClient.Timeout to 2 minutes (lines 19-31 in Program.cs) — overridden by resilience handler
  2. Set ASP.NET Core request timeout to 5 minutes (lines 55-64) — different layer
  3. Set frontend fetch timeout to 180 seconds — different layer
- **Actual Fix:** Configure `HttpStandardResilienceOptions` for the "ollamaModel" named client to extend total request timeout to 5 minutes (lines 45-49 in Program.cs)
- **Key Learning:** When using Aspire ServiceDefaults with `AddStandardResilienceHandler()`, the resilience handler's timeout takes precedence over HttpClient.Timeout. Must configure resilience options explicitly for long-running operations.
- **File:** `src/SqlAuditedQueryTool.App/Program.cs`
- **Layers of Timeout:**
  1. HttpClient.Timeout (2 minutes) — transport layer
  2. Resilience handler total request timeout (5 minutes) — **this was the bottleneck**
  3. ASP.NET request timeout (5 minutes) — server layer
  4. Frontend fetch timeout (180 seconds) — client layer

### 2025-02-23: Multi-Result Set Support — Fixed Missing Result Sets
- **Problem:** When running multiple queries (e.g., `SELECT * FROM Table1; SELECT * FROM Table2;`), only the first result set was displayed in the UI. The tabs for multiple result sets were already implemented in the frontend but never populated.
- **Root Cause:** `SqlQueryExecutor.ExecuteReadOnlyQueryAsync()` was only reading the first result set from `SqlDataReader`. It never called `reader.NextResultAsync()` to read subsequent result sets.
- **Solution Implemented:**
  1. **Backend Model Changes:**
     - Created `QueryResultSet` class to represent a single result set (columns, rows, rowCount, columnCount)
     - Modified `QueryResult` to contain `ResultSets` collection instead of single result
     - Added legacy properties (RowCount, ColumnCount, etc.) as computed properties for backward compatibility
  2. **Backend Executor Changes:**
     - Modified `SqlQueryExecutor` to loop through all result sets using `do-while` with `reader.NextResultAsync()`
     - Added comprehensive logging: logs each result set (index, row count, column count) and total summary
     - Injected `ILogger<SqlQueryExecutor>` for structured logging
  3. **API Changes:**
     - Updated `/api/query/execute` endpoint to return `resultSets` array in response
     - Maintained legacy `columns`, `rows`, `rowCount` fields for backward compatibility
     - Added logging in API endpoint to log total result set count and row count
  4. **Frontend Changes:**
     - Added console logging in `handleExecute` and `handleExecuteSelection` to log:
       - Number of result sets received from backend
       - Each result set's row count and column count
     - Frontend already had full support for displaying multiple result sets with tabs
- **Files Modified:**
  - `src/SqlAuditedQueryTool.Core/Models/QueryResult.cs` — Added QueryResultSet class, restructured QueryResult
  - `src/SqlAuditedQueryTool.Database/SqlQueryExecutor.cs` — Loop through all result sets, add logging
  - `src/SqlAuditedQueryTool.App/Program.cs` — Update API response format
  - `src/SqlAuditedQueryTool.App/ClientApp/src/App.tsx` — Add frontend logging
  - `tests/SqlAuditedQueryTool.Core.Tests/Security/AuditIntegrityTests.cs` — Update test to use new model
- **Logging Added:**
  - **Backend (per result set):** "Result set {Index}: {RowCount} rows, {ColumnCount} columns"
  - **Backend (summary):** "Query executed successfully: {ResultSetCount} result set(s), {TotalRows} total rows, {ExecutionMs}ms"
  - **Backend (API):** "API: Query executed - {ResultSetCount} result set(s), {TotalRows} total rows, {ExecutionMs}ms"
  - **Frontend (console):** "Frontend: Received {N} result set(s) from backend" + per-set details
- **Verification:** All 57 tests pass. Solution builds successfully.
- **Backward Compatibility:** Legacy single-result consumers can still access first result via computed properties

### 2026-02-23: Timeout Still Failing — ConfigureHttpClientDefaults Conflict
- **Problem:** After 4 attempts to fix 30-second timeout, still getting `Polly.Timeout.TimeoutRejectedException: The operation didn't complete within the allowed timeout of '00:00:30'` on POST /api/chat
- **Root Cause Discovery:** The previous fix (lines 45-49 in Program.cs) tried to configure `HttpStandardResilienceOptions` for the "ollamaModel" named client, but this was ineffective because:
  1. Line 16: `builder.AddServiceDefaults()` calls `ConfigureHttpClientDefaults` and adds `AddStandardResilienceHandler()` with 30-second default timeout
  2. Lines 37-43: SECOND call to `ConfigureHttpClientDefaults` was added, which was **overriding** the defaults from ServiceDefaults
  3. The second call was adding the resilience handler again but without proper configuration
  4. The named client configuration on lines 45-49 wasn't being applied because the configuration scope was wrong
- **Why Previous Fix Failed:**
  - `Configure<HttpStandardResilienceOptions>("ollamaModel", ...)` only works if the named client has its own resilience handler configured
  - Aspire's `AddServiceDefaults()` applies resilience via `ConfigureHttpClientDefaults`, which affects ALL HttpClients
  - Calling `ConfigureHttpClientDefaults` twice caused configuration conflicts
  - The configuration needed to be set BEFORE `AddServiceDefaults()` is called so it's picked up by the standard resilience handler
- **Solution:** Configure `HttpStandardResilienceOptions` globally (without named client) BEFORE calling `AddServiceDefaults()`:
  ```csharp
  builder.Services.Configure<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
  {
      options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
  });
  builder.AddServiceDefaults(); // Now picks up the 5-minute timeout configuration
  ```
- **Key Insight:** When using Aspire's `AddServiceDefaults()` which applies `ConfigureHttpClientDefaults`, you must configure options BEFORE the call, not after. Named client configuration doesn't work for options applied via defaults.
- **File Modified:** `src/SqlAuditedQueryTool.App/Program.cs` — Moved resilience configuration before AddServiceDefaults, removed duplicate ConfigureHttpClientDefaults call
- **Configuration Order Matters:**
  1. Configure options (HttpStandardResilienceOptions)
  2. Call AddServiceDefaults() (which applies the configured options)
  3. Register named clients (AddOllamaApiClient)
  4. Configure client-specific settings (HttpClient.Timeout)

### 2026-02-23: Timeout Still Failing (5th Attempt) — ConfigureAll Fix ✅ RESOLVED
- **Problem:** STILL getting 30-second timeout after previous fix. This is now the FIFTH attempt.
- **Deep Dive Discovery:** The previous fix (configuring options BEFORE AddServiceDefaults) was conceptually wrong. Here's what actually happens:
  1. `AddServiceDefaults()` (line 16) calls `ConfigureHttpClientDefaults` which registers a lambda
  2. Inside that lambda (ServiceDefaults/Extensions.cs line 29-36), it calls `http.AddStandardResilienceHandler()`
  3. That lambda executes LATER when HttpClients are created, NOT when AddServiceDefaults is called
  4. So configuring options BEFORE AddServiceDefaults doesn't help - the resilience handler gets default 30s timeout
- **Real Root Cause:** Need to use `ConfigureAll<>` instead of `Configure<>` to apply timeout to ALL resilience handlers created by the ConfigureHttpClientDefaults lambda
- **Actual Fix:** 
  ```csharp
  builder.AddServiceDefaults();
  
  // Configure resilience handler timeout AFTER AddServiceDefaults
  // Use ConfigureAll to apply to ALL HttpClients (including future ones)
  builder.Services.ConfigureAll<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
  {
      options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
  });
  
  builder.AddOllamaApiClient("ollamaModel");
  ```
- **Key Learning:** 
  - `Configure<T>()` only configures the default (unnamed) instance
  - `ConfigureAll<T>()` configures ALL instances (named and unnamed)
  - Since AddStandardResilienceHandler() creates resilience handlers per HttpClient, you need ConfigureAll
  - Order: AddServiceDefaults → ConfigureAll (resilience options) → AddOllamaApiClient
- **Files Modified:**
  - `src/SqlAuditedQueryTool.App/Program.cs` — Changed to ConfigureAll, moved AFTER AddServiceDefaults
  - Added startup logging to verify timeout configuration
- **Timeout Configuration Verified:**
  1. HttpClient.Timeout: 120 seconds (from OllamaOptions)
  2. Resilience handler total request timeout: 300 seconds (5 minutes) — **ConfigureAll fixes this** ✅
  3. ASP.NET request timeout: 300 seconds (5 minutes)
  4. Frontend fetch timeout: 180 seconds
- **Status:** ✅ PRODUCTION READY — All 57 tests pass, `/api/chat` no longer times out at 30 seconds
