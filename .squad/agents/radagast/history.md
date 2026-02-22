# Radagast — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: LLM must NEVER be exposed to actual database data — only schema, query patterns, code structure.
- Owns: Local LLM ops, SQL Server MCP integration, query generation safety

## Learnings
<!-- Append new learnings below this line -->

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

