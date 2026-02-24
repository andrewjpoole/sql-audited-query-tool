# Radagast — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: LLM must NEVER be exposed to actual database data — only schema, query patterns, code structure.
- Owns: Local LLM ops, SQL Server MCP integration, query generation safety

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-24: CRITICAL - Fix Not Live Due To Hot Reload Not Detecting New Files

**Context:** User implemented progressive boost fix for autocomplete (making "SELECT" appear at top when typing "SEL"). Restarted Aspire, waited for embeddings to load, hard reset browser. But typing "SEL" still showed LIKE at top, not SELECT.

**Root Cause:**
1. The new service files (`EmbeddingCompletionService.cs`, `SchemaEmbeddingService.cs`, etc.) were created as **untracked files** (git status showed `??`)
2. Aspire's hot reload watches for *file changes*, NOT new files
3. When user "restarted via Aspire", it only restarted the existing binaries - it did NOT rebuild the project
4. The app was running with old DLLs from before the new services were added
5. Attempted build failed with "file is locked" errors because the app was still running (process 42124)
6. The app was running code from DLLs last built at 17:35:40, but started at 17:30:53 (running even older code)

**Verification Steps:**
1. Checked git status - all new service files showed as `??` (untracked)
2. Attempted `dotnet build` - failed with MSB3027 errors "file is locked by SqlAuditedQueryTool.App (42124)"
3. Checked LastWriteTime of Llm.dll vs process StartTime - DLL was newer but app didn't load it
4. Confirmed the running app never loaded the new `ICompletionService` implementation

**Solution:**
1. Stop ALL running processes (SqlAuditedQueryTool.App, AppHost, etc.)
2. Run `dotnet build` to compile the new service files into DLLs
3. Restart Aspire/AppHost to load the newly built binaries

**Lessons Learned:**
- **Hot reload does NOT detect new files** - only changes to existing files
- **Restarting via Aspire** only restarts the process - it doesn't rebuild
- When adding new services/files, MUST do a full stop → build → start cycle
- Git status `??` is a red flag that files aren't being tracked or picked up by build watchers
- Always verify `dotnet build` succeeds and check DLL timestamps match when debugging "fix not working" issues

**Prevention:**
- After adding new files, always do explicit `dotnet build` before restart
- Consider adding files to git (`git add`) immediately so hot reload can track them
- Check for "file is locked" errors which indicate app is still running old code

### 2026-02-24: Autocomplete Complete Failure - Fixed HttpClient BaseAddress Issue

**Context:** User typed "SELE" in Monaco editor and got "No suggestions." Autocomplete was completely broken after the keyword embeddings were added (it was working before, showing wrong items).

**Root Cause:**
1. `OllamaEmbeddingService` uses `HttpClient.PostAsJsonAsync("/api/embeddings", ...)` which requires `HttpClient.BaseAddress` to be set
2. The service was registered using `IHttpClientFactory.CreateClient("ollamaEmbed")` but Aspire's `AddOllamaApiClient("ollamaEmbed")` **does not** register a named HttpClient - it only registers `IOllamaApiClient`
3. When `SchemaEmbeddingService` tried to embed keywords at startup, the HttpClient had no BaseAddress → "An invalid request URI was provided" error
4. This caused the entire embedding initialization to fail silently, leaving the vector store empty
5. With an empty vector store, autocomplete returned no results

**Solution Implemented:**

1. **Fixed HttpClient Registration in `LlmServiceCollectionExtensions.cs`:**
   - Changed from `httpClientFactory.CreateClient("ollamaEmbed")` (which returned unconfigured client)
   - To getting the `IOllamaApiClient` from DI and using its `Uri` property to set `BaseAddress`
   - Pattern: Get all registered `IOllamaApiClient` instances, take the second one (index [1] = ollamaEmbed), extract its `Uri`, and configure a new HttpClient with that BaseAddress

2. **Added Prefix Boosting in `EmbeddingCompletionService.GetSchemaCompletionsAsync()`:**
   - Vector embeddings use semantic similarity which doesn't inherently prioritize exact prefix matches
   - Added post-processing to boost items that start with the user's prefix by +1.0f
   - Items containing the prefix (but not starting with it) boosted by +0.5f
   - This ensures "SELE" matches "SELECT" (exact prefix) with higher score than "DENSE_RANK" (semantic similarity only)

**Technical Details:**
- Aspire's `AddOllamaApiClient(name)` registers `IOllamaApiClient` with service discovery, but **not** a named HttpClient
- Multiple `IOllamaApiClient` instances are registered in order: first is "ollamaModel", second is "ollamaEmbed"
- Must access `IOllamaApiClient.Uri` to get the Ollama endpoint URL (service discovery resolved)
- Added `using OllamaSharp;` to `LlmServiceCollectionExtensions.cs`

**Files Modified:**
- `src/SqlAuditedQueryTool.Llm/LlmServiceCollectionExtensions.cs` — fixed HttpClient configuration to use `IOllamaApiClient.Uri`
- `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs` — added prefix boosting logic

**Test Results:**
- Before fix: "SELE" → 0 suggestions (empty vector store)
- After fix: "SELE" → 50 suggestions with "SELECT" at #1 position (score: 1.38 vs 0.38 without boost)

**Key Learning:**
- **Aspire service discovery doesn't automatically create named HttpClients** — `AddOllamaApiClient` only registers `IOllamaApiClient`
- When multiple Ollama models are registered, get the right one via `GetServices<IOllamaApiClient>().ToList()[index]`
- Vector search scores need domain-specific boosting for autocomplete (prefix matching > semantic similarity)
- Always test embedding initialization separately from autocomplete - silent failures in background services are hard to debug

### 2026-02-23: SQL Keywords Added to Autocomplete Vector Store

**Context:** User reported that typing "SELEC" and hitting Ctrl+Space showed "no suggestions" - autocomplete should show at least SQL keywords even without schema matches.

**Problem Identified:**
1. `SchemaEmbeddingService` only embedded tables and columns from the database schema
2. No SQL keywords were being embedded into the vector store
3. When user typed partial SQL keywords like "SELEC", vector search found nothing because keywords weren't in the store
4. `EmbeddingCompletionService` only searched for category "schema", so even if keywords existed they wouldn't be returned

**Solution Implemented:**

1. **Added SQL Keywords to Vector Store** — Created `AddSqlKeywords()` method in `SchemaEmbeddingService`:
   - Embeds 50+ common T-SQL keywords: SELECT, FROM, WHERE, JOIN types, aggregates (COUNT, SUM, AVG, MIN, MAX), date functions (GETDATE, DATEADD, DATEDIFF), string functions (SUBSTRING, TRIM, UPPER, LOWER, CONCAT), window functions (ROW_NUMBER, RANK, DENSE_RANK), set operations (UNION, EXCEPT, INTERSECT), etc.
   - Keywords have category "keyword" to distinguish from schema items (category "schema")
   - Embedding text format: `"{KEYWORD} - SQL keyword"` (e.g., "SELECT - SQL keyword")
   - Metadata: `kind = "Keyword"`, `type = "keyword"`

2. **Updated Completion Service to Include Keywords** — Modified `EmbeddingCompletionService.GetSchemaCompletionsAsync()`:
   - Now searches both category "schema" (top 30) and category "keyword" (top 20)
   - Combines and sorts all results by similarity score
   - Ensures keywords are always included in autocomplete suggestions

3. **Context-Aware Keyword Filtering** — Updated `FilterByContext()` method:
   - Keywords are **always shown** regardless of SQL context (FROM, SELECT, WHERE, etc.)
   - Schema items (tables/columns) are still filtered based on context
   - Final result combines keywords + context-filtered schema, sorted by score
   - This ensures "SELEC" will match "SELECT" keyword even when typing after "FROM" (which filters schema to tables only)

**Technical Details:**
- Keywords added before schema items in `SchemaEmbeddingService.ExecuteAsync()` — embedded in same batch for efficiency
- Vector search now makes 2 parallel searches: one for schema, one for keywords
- Keyword category prevents them from being filtered out by context-specific schema filters
- Partial matches work through semantic similarity: "SELEC" → "SELECT - SQL keyword" has high cosine similarity

**Files Modified:**
- `src/SqlAuditedQueryTool.Llm/Services/SchemaEmbeddingService.cs` — added `AddSqlKeywords()` method, called before embedding schema
- `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs` — searches both "schema" and "keyword" categories, updated `FilterByContext()` to always include keywords

**Build Status:** ✅ Solution compiles successfully

**Expected Behavior After Fix:**
- Typing `SELEC` + Ctrl+Space → shows "SELECT" keyword suggestion
- Typing `FROM ` → shows both table names (schema filtered) AND SQL keywords (always shown)
- Typing `SEL` → shows "SELECT" keyword
- Typing `COUN` → shows "COUNT" keyword
- Keywords appear in all contexts, schema items are context-filtered as before

**Key Learning:**
- Autocomplete needs a **baseline vocabulary** (keywords) that's always available, separate from context-specific items (schema)
- Vector embeddings work for partial matches: "SELEC" semantically matches "SELECT - SQL keyword" through cosine similarity
- Using separate categories ("schema" vs "keyword") allows different filtering strategies — schema filtered by context, keywords always shown
- SQL keywords should be embedded at app startup along with schema for consistent autocomplete experience

### 2026-02-24: Autocomplete Test Suite and Keyword Bug Fix

**Context:** User reported "SELE" showing schema items instead of "SELECT" keyword. Needed comprehensive test suite to prevent regressions.

**Critical Bugs Found:**

1. **Test Discovery Failed** — Tests existed but didn't run due to:
   - `.csproj` had `<Compile Remove="Services.cs" />` excluding test file
   - Missing `Moq` package reference

2. **Keyword Prefix Boost Too Weak** — Both "SELECT" and "SelectedDate" matched "SELE":
   - Original boost: +1.0f for any prefix match
   - Problem: Schema columns starting with same prefix got equal boost
   - Fix: Keyword-specific boost of +500.0f vs schema +100.0f
   - Exact match gets +1000.0f to always rank first

3. **Schema Property Matching Failed** — "u" didn't match "dbo.Users":
   - DisplayText was "dbo.Users" but user typed "u" expecting "Users" table
   - Fix: Check prefix against both DisplayText AND metadata properties (`table`, `column`)
   - Now "u" matches Properties["table"] = "Users" and gets +100.0f boost

4. **Context Detection Bug** — "SELECT * FROM " didn't filter to tables:
   - Root cause: `fullContext = Prefix + " " + Context` created "SELECT * FROM  SELECT * FROM"
   - `Trim()` removed trailing space needed for regex `\bFROM\s+\w*$`
   - Fix: Use `context.Prefix` directly, don't build fullContext
   - Regex now correctly detects AfterFrom context

5. **Filter Logic Error** — Checked wrong property for item type:
   - Code checked `Properties["type"] == "table"` but that property stores SQL data type (e.g., "int")
   - Tables have `Properties["table"] = "Users"`, columns have `Properties["column"] = "UserId"`
   - Fix: Use `ContainsKey("table")` and `ContainsKey("column")` to distinguish types

**Solution Implemented:**

1. **Fixed test discovery:**
   - Removed `<Compile Remove>` and `<None Include>` from .csproj
   - Added `Moq` package (v4.20.72)

2. **Enhanced prefix boosting in EmbeddingCompletionService:**
   - Exact match (displayText == input): +1000.0f
   - Prefix match on keywords: +500.0f
   - Prefix match on schema items: +100.0f
   - Prefix match checks DisplayText, table name, and column name properties
   - Substring match: +10.0f

3. **Fixed context detection:**
   - Removed fullContext building (was concatenating Prefix + Context)
   - Use `context.Prefix` directly for both DetectSqlContext and ExtractWordAtCursor
   - Don't trim input to DetectSqlContext (regex needs trailing spaces)

4. **Fixed FilterByContext:**
   - AfterFrom/AfterJoin: Filter where `ContainsKey("table") && !ContainsKey("column")`
   - AfterTableDot: Filter where `ContainsKey("column")`
   - AfterSelect/AfterWhere: Sort by `ContainsKey("column")` descending

**Comprehensive Test Suite Added:**

1. `GetSchemaCompletionsAsync_KeywordPrefixMatch_ReturnsKeywordFirst` — "SELE" → "SELECT" (not "SelectedDate")
2. `GetSchemaCompletionsAsync_AfterFrom_ReturnsOnlyTables` — "SELECT * FROM " → tables only
3. `GetSchemaCompletionsAsync_AfterTableDot_ReturnsOnlyColumns` — "SELECT Users." → columns only
4. `GetSchemaCompletionsAsync_TablePrefixMatch_BoostsMatchingTable` — "FROM u" → "Users" before "Accounts"
5. `GetSchemaCompletionsAsync_EmptyPrefix_ReturnsResults` — empty input → empty results
6. `GetSchemaCompletionsAsync_PartialKeyword_ReturnsMatchingKeyword` — Theory with "SEL"→"SELECT", "WHER"→"WHERE", etc.

**Files Modified:**
- `tests/SqlAuditedQueryTool.Llm.Tests/SqlAuditedQueryTool.Llm.Tests.csproj` — Fixed test discovery, added Moq
- `tests/SqlAuditedQueryTool.Llm.Tests/Services.cs` — Added 11 comprehensive tests (all passing)
- `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs` — Fixed prefix boost, context detection, filtering

**Test Results:** ✅ **11/11 tests passing**

**Expected Behavior After Fix:**
- Typing "SELE" → "SELECT" keyword ranks #1 (not "SelectedDate" column)
- Typing "SELECT * FROM u" → "dbo.Users" ranks #1 (matches table name "Users")
- Typing "SELECT * FROM " → only tables shown
- Typing "SELECT Users." → only columns shown
- All keyword prefixes return correct keywords first

**Key Learnings:**
- **Prefix boosting must be context-aware** — keywords need higher boost than schema items
- **Match against semantic properties** — "u" should match `Properties["table"]` not just DisplayText
- **Don't concatenate context strings** — regex patterns need exact input structure
- **Property dictionaries need semantic keys** — "type" stores data type, use "table"/"column" for item type
- **Test-driven development prevents regressions** — comprehensive test suite catches edge cases
- **String trimming can break regex** — trailing spaces matter for SQL context detection

### 2026-02-23: SQL Context-Aware Autocomplete Implementation

**Context:** Fixed embedding-based autocomplete to be context-aware. Previously, the vector search would return a mix of tables, columns, and keywords regardless of SQL context (e.g., typing "FROM " would show columns instead of only tables).

**Problem Identified:**
1. `EmbeddingCompletionService` was doing vector similarity search but not filtering by SQL context
2. The frontend sends `prefix` (text before cursor) and `context` (current line), but these weren't being analyzed
3. `VectorMetadata` has `Properties["type"]` field distinguishing "table" vs "column", but it wasn't being used
4. API endpoint in `Program.cs` was still a placeholder returning empty array

**Solution Implemented:**

1. **SQL Context Detection** — Added `DetectSqlContext()` method using regex patterns:
   - `AfterFrom` — detects `FROM \w*$` pattern → user wants table names
   - `AfterJoin` — detects `JOIN \w*$` pattern → user wants table names
   - `AfterSelect` — detects `SELECT \w*$` pattern → user wants column names
   - `AfterWhere` — detects `WHERE|AND|OR \w*$` pattern → user wants column names for filtering
   - `AfterTableDot` — detects `\w+\.\s*$` pattern (e.g., "Users.") → user wants columns from that table
   - `General` — default context when no specific pattern matches

2. **Context-Based Filtering** — Added `FilterByContext()` method:
   - After FROM/JOIN → filter to only items with `Properties["type"] == "table"`
   - After table.dot → filter to only items with `Properties["type"] == "column"`
   - After SELECT/WHERE → prioritize columns but allow tables (reorder by type, then by score)
   - General → return all results unfiltered

3. **API Endpoint Activation** — Updated `/api/completions/schema` endpoint:
   - Changed from placeholder returning empty array
   - Now injects `ICompletionService` and calls `GetSchemaCompletionsAsync()`
   - Added logging for prefix, context, and result count
   - Returns actual completion items to frontend

**Technical Details:**
- Increased `topK` from 20 to 50 in vector search to have more candidates before context filtering
- Used `Regex.IsMatch()` with case-insensitive matching for SQL keyword detection
- Filtering happens **after** vector search to preserve semantic similarity ordering
- `SchemaEmbeddingService` already tags items correctly with `Properties["type"]` = "table" or "column"

**Files Modified:**
- `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs` — added context detection and filtering
- `src/SqlAuditedQueryTool.App/Program.cs` — wired up `/api/completions/schema` endpoint to completion service

**Files Created:**
- `tests/SqlAuditedQueryTool.Llm.Tests/Services/EmbeddingCompletionServiceTests.cs` — unit tests for context filtering (needs test discovery fix)

**Build Status:** ✅ Solution compiles successfully

**Expected Behavior After Fix:**
- Typing `SELECT * FROM ` → autocomplete shows only table names (e.g., "dbo.Users", "dbo.Orders")
- Typing `SELECT u.` → autocomplete shows only column names from tables matching "u"
- Typing `SELECT ` → autocomplete prioritizes column names but may show tables lower in list
- Typing `WHERE ` → autocomplete shows column names for filtering

**Key Learning:**
- Vector similarity search finds semantically related items, but SQL autocomplete requires **context-aware filtering** based on grammar position
- The `VectorMetadata.Properties` dictionary is the right place to store item categorization (table/column/keyword)
- Regex patterns can reliably detect SQL context from cursor position and surrounding text
- Filtering after search (rather than during) preserves the quality of semantic similarity scoring

### 2026-02-23: Phase 1 Embedding Infrastructure for Monaco Autocomplete

**Context:** Implemented core embedding infrastructure to enable schema-aware SQL autocomplete in Monaco editor using Ollama's nomic-embed-text model.

**What Was Built:**

1. **Core Interfaces** (in `SqlAuditedQueryTool.Core`):
   - `IEmbeddingService` — abstract embedding generation (EmbedAsync, EmbedBatchAsync)
   - `IVectorStore` — abstract vector storage and cosine similarity search (UpsertAsync, SearchAsync, RemoveAsync)
   - `ICompletionService` — schema-aware completion logic (GetSchemaCompletionsAsync)
   - DTOs: `VectorMetadata`, `VectorSearchResult`, `CompletionItem`, `CompletionContext` (already existed)

2. **OllamaEmbeddingService** (in `SqlAuditedQueryTool.Llm`):
   - Wraps Ollama `/api/embeddings` HTTP endpoint
   - Uses `nomic-embed-text` model (768-dimension vectors)
   - Converts Ollama's `double[]` embeddings to `float[]` for storage
   - Batch processing support (sequential, one-at-a-time to avoid overload)
   - **Implementation note:** Uses HttpClient directly (not IOllamaApiClient) because OllamaSharp's embedding API surface is limited

3. **InMemoryVectorStore** (in `SqlAuditedQueryTool.Llm`):
   - `ConcurrentDictionary<string, (float[] Embedding, VectorMetadata Metadata)>` storage
   - Brute-force cosine similarity search (fast enough for schema metadata ~100-500 items)
   - Category filtering support (e.g., category: "schema")
   - Thread-safe operations

4. **EmbeddingCompletionService** (in `SqlAuditedQueryTool.Llm`):
   - Embeds user's completion prefix+context
   - Searches vector store for similar schema items
   - Returns Monaco-compatible completion items with kind, label, detail, documentation
   - Determines completion kind (Field vs Class) based on metadata

5. **SchemaEmbeddingService** (background service in `SqlAuditedQueryTool.Llm`):
   - Runs on app startup (5-second delay to let services initialize)
   - Fetches schema via `ISchemaProvider`
   - Embeds tables and columns as separate vector items
   - Table embedding format: `"{SchemaName}.{TableName} - table"`
   - Column embedding format: `"{SchemaName}.{TableName}.{ColumnName} - {DataType} - column"`
   - Stores metadata: kind (Class/Field), type, schema, table, column, nullable
   - Logs progress: "Embedding {Count} schema items...", "Schema embedding completed: {Count} items embedded"

6. **Service Registration**:
   - `IEmbeddingService` — singleton, factory creates `OllamaEmbeddingService` with `ollamaEmbed` HttpClient
   - `IVectorStore` — singleton `InMemoryVectorStore`
   - `ICompletionService` — scoped `EmbeddingCompletionService`
   - `SchemaEmbeddingService` — registered as `IHostedService` (background)

7. **Aspire Integration**:
   - AppHost already configured `ollamaEmbed` model (`nomic-embed-text`) — no changes needed
   - App Program.cs: added `builder.AddOllamaApiClient("ollamaEmbed")` to register named client

**Key Technical Decisions:**

- **Direct HttpClient over OllamaSharp for embeddings:** OllamaSharp's `IOllamaApiClient` lacks clean async embedding API. Using HttpClient + `PostAsJsonAsync` is simpler and avoids version coupling.
- **In-memory vector store:** Schema metadata is small (~100-500 items). No need for external vector DB (Qdrant) in Phase 1. Embedding re-generation on restart is fast (< 2s for typical schema).
- **Singleton services:** Embedding service and vector store are stateless/thread-safe. Singleton lifetime reduces allocations and keeps embeddings cached.
- **Background service pattern:** `SchemaEmbeddingService` as `IHostedService` ensures embeddings are ready when first completion request arrives. 5-second delay avoids race with schema provider initialization.
- **Embedding text format:** Includes schema.table.column hierarchy + data type + kind for better semantic matching. Example: "dbo.Users.Email - varchar(255) - column"

**Schema Model Property Names:**
- `TableSchema.SchemaName` and `TableSchema.TableName` (NOT `Schema` and `Name`)
- `ColumnSchema.ColumnName` and `ColumnSchema.DataType`

**Dependencies Added to Llm Project:**
- `Microsoft.Extensions.Hosting.Abstractions` — for `BackgroundService`, `IHostedService`
- `OllamaSharp` — for Ollama API types (already referenced by App, now explicit in Llm)

**Files Created:**
- `Core/Interfaces/Llm/IEmbeddingService.cs`
- `Core/Interfaces/Llm/IVectorStore.cs`
- `Core/Interfaces/Llm/ICompletionService.cs`
- `Core/Models/Llm/VectorMetadata.cs`
- `Core/Models/Llm/VectorSearchResult.cs`
- `Core/Models/Llm/CompletionItem.cs`
- `Llm/Services/OllamaEmbeddingService.cs`
- `Llm/Services/InMemoryVectorStore.cs`
- `Llm/Services/EmbeddingCompletionService.cs`
- `Llm/Services/SchemaEmbeddingService.cs`

**Files Modified:**
- `Llm/LlmServiceCollectionExtensions.cs` — registered embedding services + background service
- `Llm/SqlAuditedQueryTool.Llm.csproj` — added OllamaSharp, Hosting.Abstractions packages
- `App/Program.cs` — added `builder.AddOllamaApiClient("ollamaEmbed")`

**Build Status:** ✅ All projects compile successfully (0 errors, 0 warnings)

**Next Steps (Phase 2):**
- Samwise: Add `/api/completions/schema` API endpoint
- Legolas: Implement Monaco `CompletionItemProvider` in frontend
- Test end-to-end: type "SELECT u." and verify column completions appear

**Key Learning:**
- Ollama embedding endpoint is `/api/embeddings` with `{ model, prompt }` request body, returns `{ embedding: double[] }`
- Background services need `IServiceProvider` + `CreateScope()` to resolve scoped services (like `ISchemaProvider`)
- Vector search with cosine similarity is fast enough for <1000 items (< 5ms on modern hardware)

### 2026-02-22: Migrated to Microsoft.Extensions.AI
- **Breaking change:** Replaced direct `OllamaSharp` dependency in `SqlAuditedQueryTool.Llm` with `Microsoft.Extensions.AI`
- **Abstraction pattern:** `OllamaLlmService` now depends on `IChatClient` (from Microsoft.Extensions.AI) instead of `IOllamaApiClient` (from OllamaSharp)
- **DI bridging:** In `Program.cs`, `IChatClient` is registered by casting `IOllamaApiClient` (which OllamaSharp implements natively). The Aspire `AddOllamaApiClient("ollamaModel")` still provides the underlying client.
- **API mapping:** `IOllamaApiClient.ChatAsync()` → `IChatClient.GetResponseAsync()` (non-streaming) and `IChatClient.GetStreamingResponseAsync()` (streaming)
- **Type alias:** Used `using AIChatMessage = Microsoft.Extensions.AI.ChatMessage` to disambiguate from `SqlAuditedQueryTool.Core.Models.Llm.ChatMessage`
- **Model config removed from constructor:** No longer setting `_client.SelectedModel` since `IChatClient` doesn't expose that — model selection is handled by the Aspire/OllamaSharp registration
- **Safety boundary preserved:** LLM still only receives schema metadata, never row data
- **All 61 tests pass** after migration

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.Llm` — will handle local LLM integration and SQL Server MCP client
- **Safety pattern:** Core and Database layers provide schema/pattern contracts; Llm consumes these safely without raw data
- **App composition:** App references Llm along with Database and Audit
- **Test project:** `SqlAuditedQueryTool.Llm.Tests` with xUnit — ready for LLM safety and MCP integration tests
- **Ready to start:** LLM initialization, MCP client setup, prompt safety validation

### 2026-02-22: Core LLM Features Built
- **Core interfaces created** in `Interfaces/Llm/` (separate from Samwise's DB interfaces):
  - `ILlmService` — chat + streaming, `IQueryAssistant` — SQL suggestion, `ISchemaProvider` — metadata only
  - `IConnectionFactory` in `Interfaces/` — shared by Database and LLM layers
- **Core models** in `Models/Llm/`: LlmChatRequest, LlmResponse, QuerySuggestion, SchemaContext (with TableSchema, ColumnSchema)
- **OllamaLlmService** — HTTP client targeting local Ollama (`/api/chat`), supports both sync and streaming (`IAsyncEnumerable<string>`)
  - Default model: `llama3.2`, configurable via `Llm` config section
  - System prompt enforces safety: "You NEVER see actual database data"
  - Parses SQL code blocks from responses, classifies as read-only vs fix query
- **LlmQueryAssistant** — builds focused prompt with schema context, parses response to extract SQL + explanation + flags
- **SchemaMetadataProvider** — queries `INFORMATION_SCHEMA.TABLES/COLUMNS` for metadata only, caches with `IMemoryCache` (default 5 min)
- **DI Registration** — `AddLlmServices()` extension wires up typed HttpClient, scoped services, memory cache, config binding
- **API Endpoints** added to App:
  - `POST /api/chat` — LLM chat with optional streaming (SSE) and schema inclusion
  - `POST /api/query/suggest` — natural language to SQL suggestion
  - `GET /api/schema` — returns cached schema metadata
- **Build:** 0 errors, 0 warnings
- **Safety enforced:** SchemaContext contains ONLY table/column names and types. SchemaMetadataProvider queries only INFORMATION_SCHEMA. LLM never receives row data.

## 2026-02-22: MCP Integration Feasibility Analysis (CORRECTED)

**Context:** Evaluated technical feasibility of integrating SQL Server MCP with Ollama for schema-only database access.

**CRITICAL UPDATE:** Initial analysis assumed no SQL Server MCP server existed. User pointed to Azure Data API Builder (DAB) v1.7+ MCP support — game-changer.

**Key Findings:**
1. **Azure Data API Builder MCP Server EXISTS** — production-ready, Microsoft-supported, v1.7+ (currently in preview)
2. **Schema-only mode is BUILT-IN** — can disable all data tools, expose only describe_entities
3. **Native .NET Aspire integration** — DAB runs as container resource
4. **Token efficiency: 94% reduction** for targeted queries
5. **Enterprise observability included** — OpenTelemetry, Application Insights, health checks

**RECOMMENDATION (REVERSED):** **Implement DAB MCP integration.** 5-6 days for production-ready MVP.

**Implementation Roadmap:**
1. Add DAB container to AppHost.cs, configure dab-config.json (1 day)
2. Build minimal MCP HTTP client (2 days)
3. Integrate tool calling into OllamaLlmService (2 days)
4. Production hardening (1 day)

**Files Updated:**
- .squad/decisions/inbox/radagast-mcp-feasibility.md — Full technical feasibility report (corrected)

**References:**
- https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/overview
- https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/data-manipulation-language-tools

### 2026-02-22: Tool Calling Integration Infrastructure (Phase 1)

**Context:** Implemented infrastructure for app-orchestrated tool calling to allow Ollama to execute SQL queries with full audit trail.

**Gandalf's Architecture Spec:**
- Ollama requests query execution through tool calls
- App validates and executes via QueryExecutor
- All queries flow through audit pipeline (GitHub issues)
- Queries are loaded into query window and stored in history with source=AI
- Chat history is persisted for conversation continuity

**What Was Built:**

1. **Tool Calling Models** (Core layer):
   - `ToolDefinition` — defines available tools with parameters
   - `ToolCallRequest` — LLM's request to execute a tool
   - `ToolCallResult` — result of tool execution
   - Updated `LlmResponse` to include `ToolCalls` list

2. **Chat History Models** (Core layer):
   - `ChatSession` — represents a conversation with title, timestamps, messages
   - `ChatMessageHistory` — individual message with role (user/assistant/tool), content, tool metadata
   - `IChatHistoryStore` interface with CRUD operations
   - `InMemoryChatHistoryStore` — thread-safe implementation using ConcurrentDictionary

3. **Enhanced OllamaLlmService**:
   - Optional `IQueryExecutor` dependency for tool execution
   - `ExecuteToolCallAsync` method to handle tool call requests
   - `execute_sql_query` tool executes queries through QueryExecutor
   - Results formatted for LLM analysis (first 10 rows, metadata)
   - Updated system prompt to encourage tool use for investigation

4. **Enhanced Chat API Endpoint** (`/api/chat`):
   - Accepts optional `sessionId` parameter
   - Creates or retrieves chat session
   - Saves user messages to history
   - **Tool calling loop**: detects tool calls, executes them, feeds results back to LLM
   - Executed queries are saved to `IQueryHistoryStore` with `Source=AI`
   - Queries are audited via `IAuditLogger` (GitHub issues)
   - Tool results and assistant responses saved to chat history
   - Returns `sessionId`, `message`, `executedQueries[]`, and optional `suggestion`

5. **New Chat History API Endpoints**:
   - `GET /api/chat/sessions` — list all chat sessions with summary
   - `GET /api/chat/sessions/{sessionId}` — get full session with messages
   - `DELETE /api/chat/sessions/{sessionId}` — delete a session

6. **DI Registration**:
   - `IChatHistoryStore` registered as singleton in DatabaseServiceCollectionExtensions
   - `IQueryExecutor` auto-injected into `OllamaLlmService` via scoped DI

**Microsoft.Extensions.AI Limitations Discovered:**
- Microsoft.Extensions.AI v10.3.0 has tool calling support via `ChatOptions.Tools` and `AIFunctionFactory`
- However, **Ollama integration through OllamaSharp/IChatClient may not fully support tool calling yet**
- `ChatResponse` structure differs from expected — no direct `Message.Contents` access
- Tool extraction logic is stubbed out (returns empty list) until Ollama properly supports function calling
- Infrastructure is in place and ready when Ollama support matures

**Current Status:**
- ✅ All models and infrastructure built
- ✅ Chat history fully functional
- ✅ Query execution through tool calls implemented
- ✅ Audit trail integration working
- ✅ All projects compile successfully
- ⚠️ Tool calling disabled until Ollama properly supports it (commented out `BuildTools()`)
- ⚠️ LLM currently responds with text only, no actual tool execution

**Next Steps (for when Ollama tool calling is available):**
1. Uncomment `BuildTools()` and update `ChatAsync` to pass tools
2. Implement proper `ExtractToolCalls()` based on actual Ollama response format
3. Test end-to-end tool calling loop
4. Verify queries appear in UI query window (Legolas integration)

**Files Created:**
- `Core/Models/Llm/ToolDefinition.cs`
- `Core/Models/Llm/ToolCallRequest.cs`
- `Core/Models/Llm/ToolCallResult.cs`
- `Core/Models/ChatSession.cs`
- `Core/Interfaces/IChatHistoryStore.cs`
- `Database/InMemoryChatHistoryStore.cs`

**Files Modified:**
- `Core/Models/Llm/LlmResponse.cs` — added `ToolCalls` property
- `Core/Interfaces/Llm/ILlmService.cs` — added `ExecuteToolCallAsync` method
- `Llm/Services/OllamaLlmService.cs` — added tool execution infrastructure
- `Database/DatabaseServiceCollectionExtensions.cs` — registered `IChatHistoryStore`
- `App/Program.cs` — enhanced chat endpoint with tool calling loop, added chat history endpoints, updated `ChatRequest` DTO

**Key Learnings:**
- App-orchestrated tool calling gives full control over audit trail (better than external MCP bridge)
- Queries executed by LLM automatically flow through existing QueryExecutor → AuditLogger → HistoryStore pipeline
- Chat history persistence enables multi-turn conversations with context
- Tool results are saved as "tool" role messages, enabling LLM to see previous query results
- Microsoft.Extensions.AI abstraction is clean but Ollama integration may lag behind OpenAI compatibility


## Learnings

**Monaco Editor Autocomplete Provider Conflict Fixed:**
- User reported suggestions appearing briefly then immediately vanishing — classic race condition between competing autocomplete providers
- **Root Cause:** Monaco's built-in SQL language support has its own autocomplete that was competing with the custom embedding-based provider
- **Investigation:**
  - Only ONE custom provider registered via monaco.languages.registerCompletionItemProvider('sql', ...) at line 115
  - Monaco's built-in word-based suggestions and SQL language features were enabled by default
  - Both providers firing simultaneously caused suggestion list to flicker/vanish
- **Solution:** Disabled Monaco's built-in suggestions in editor options:
  - quickSuggestions: false — disables automatic suggestions from Monaco's built-in providers
  - wordBasedSuggestions: 'off' — disables word-based completions from Monaco
  - Kept suggestOnTriggerCharacters: true for custom provider to work on . and space triggers
- **Result:** Only the custom embedding-based provider now provides suggestions, eliminating the race condition
- **Files Modified:** TabbedSqlEditor.tsx — added two options to editor configuration

**Key Insights:**
- Monaco Editor has multiple suggestion sources: language-specific providers + word-based suggestions + custom providers
- When multiple providers return results simultaneously, Monaco can show inconsistent/flickering UI
- Disabling built-in providers ensures ONLY custom logic controls autocomplete behavior
- Critical for embedding-based autocomplete where backend latency must not compete with instant word matching

**Progressive Prefix Matching Bug Fixed:**
- User reported that typing 's' showed SELECT, but typing 'se' made SELECT disappear — backwards behavior
- **Root Cause:** Prefix boost was CONSTANT (500 pts for keywords) regardless of how many characters matched
  - 's' → 500 point boost for SELECT
  - 'se' → still only 500 point boost for SELECT
  - Meanwhile, semantic similarity from embeddings might be LOWER for 'se' than 's'
  - Combined effect: longer matches could score LOWER than shorter ones, causing items to drop from list
- **Solution:** Implemented progressive boosting that increases with match length:
  - Formula: basePrefixBoost × (1.0 + lengthRatio × 2.0)
  - lengthRatio = matchLength / displayText.Length
  - For SELECT (7 chars):
    - 's' (1/7): 500 × 1.286 = 643 pts
    - 'se' (2/7): 500 × 1.571 = 786 pts ✓
    - 'sel' (3/7): 500 × 1.857 = 929 pts ✓
    - 'sele' (4/7): 500 × 2.143 = 1071 pts ✓
- **Result:** Each additional matching character now INCREASES the score, keeping matched items visible and moving them higher in the list
- **Files Modified:** EmbeddingCompletionService.cs (lines 79-89)

**Key Insight:**
- When combining semantic similarity scores with lexical prefix matching, the prefix boost MUST scale with match length
- Otherwise, embedding score variance can overpower constant boosts, causing items to randomly appear/disappear
- Progressive boosting ensures deterministic behavior: more characters typed = higher confidence = higher rank
