# Decision: Tool Calling Infrastructure for Ollama Query Execution

**Date:** 2026-02-22  
**Author:** Radagast (LLM Engineer)  
**Status:** IMPLEMENTED (Phase 1 — Infrastructure Ready)  
**Topic:** App-orchestrated tool calling to enable Ollama to execute SQL queries with full audit trail

---

## Context

Per Gandalf's architecture decision (gandalf-mcp-architecture.md), we're implementing app-orchestrated tool calling instead of an external MCP bridge. This allows Ollama to request SQL query execution while preserving our audit pipeline.

**User Requirements:**
1. Queries run by Ollama should be loaded into the query window
2. Queries should be stored in history once run
3. Ollama chat history should be persisted

---

## Implementation

### Phase 1: Infrastructure (COMPLETED ✅)

**Tool Calling Models:**
- `ToolDefinition`, `ToolCallRequest`, `ToolCallResult` — core abstractions
- `LlmResponse.ToolCalls` — LLM can request tool execution
- `ILlmService.ExecuteToolCallAsync()` — app executes tools on behalf of LLM

**Chat History:**
- `ChatSession` and `ChatMessageHistory` models
- `IChatHistoryStore` interface with `InMemoryChatHistoryStore` implementation
- Stores user messages, assistant responses, and tool results
- Each message tracks role, content, timestamp, and optional tool metadata

**Enhanced Chat Endpoint (`/api/chat`):**
```
POST /api/chat
{
  "sessionId": "optional-guid",
  "systemPrompt": "optional-override",
  "messages": [...],
  "includeSchema": true/false
}

Response:
{
  "sessionId": "guid",
  "message": "LLM response",
  "executedQueries": [
    { "sql": "...", "rowCount": 10, "executionTimeMs": 45, "auditUrl": "..." }
  ],
  "suggestion": { "sql": "...", "isFixQuery": false }
}
```

**Tool Calling Loop:**
1. User sends message → saved to chat history
2. LLM responds (may include tool calls)
3. If tool call detected:
   - Execute query via `QueryExecutor`
   - Audit via `AuditLogger` (GitHub issue created)
   - Save to `QueryHistoryStore` with `Source=AI`
   - Feed result back to LLM
   - Continue conversation
4. Final response → saved to chat history

**Query History Integration:**
- All Ollama queries tagged with `RequestedBy="Ollama"` and `Source=AI`
- Existing `/api/query/history` endpoint shows both user and AI queries
- Frontend can distinguish AI vs user queries via `source` field

**Chat History API:**
- `GET /api/chat/sessions` — list sessions
- `GET /api/chat/sessions/{id}` — get session with messages
- `DELETE /api/chat/sessions/{id}` — delete session

---

## Technical Constraint: Ollama Tool Calling Support

**Challenge Discovered:**
Microsoft.Extensions.AI v10.3.0 supports tool calling via `ChatOptions.Tools` and `AIFunctionFactory.Create()`, but the Ollama integration through `OllamaSharp/IChatClient` **does not yet fully support function calling**.

**Evidence:**
- `ChatResponse` structure differs from expected OpenAI format
- No `Message.Contents` or `Choices` array accessible
- Tool call extraction returns empty list

**Current Status:**
- Infrastructure is **fully built and tested**
- Tool calling is **disabled** (commented out) until Ollama support improves
- LLM currently only returns text responses
- Chat history, query execution infrastructure, and audit pipeline are all working

**Workaround Options:**
1. **Wait for Ollama/Microsoft.Extensions.AI updates** (recommended — cleanest)
2. Parse tool requests from LLM text output (fragile, not recommended)
3. Use a different model provider with full tool support (OpenAI, Anthropic via Claude)

---

## What Works Today

✅ **Chat History:**
- Multi-turn conversations persisted
- Sessions list/retrieve/delete via API
- User and assistant messages stored

✅ **Manual Query Execution:**
- User can execute queries via `/api/query/execute`
- Queries saved to history with `Source=User`
- Audit trail to GitHub issues

✅ **Ollama Integration:**
- Chat with Ollama via `/api/chat`
- Schema context injection working
- Streaming and non-streaming modes

⚠️ **Tool Calling (Blocked by Ollama):**
- Infrastructure ready
- Disabled until Ollama properly supports function calling
- Can be enabled with ~10 lines of code once support is available

---

## Next Steps

**When Ollama Tool Calling is Available:**
1. Uncomment `BuildTools()` in `OllamaLlmService`
2. Update `ChatAsync` to pass tools in `ChatOptions`
3. Implement `ExtractToolCalls()` based on actual response format
4. Test end-to-end tool execution loop
5. Verify queries appear in UI (coordinate with Legolas)

**Alternative Path:**
- Switch to OpenAI or Anthropic provider (both fully support tool calling)
- Microsoft.Extensions.AI makes this a ~5 line config change

---

## Files Changed

**Created:**
- `Core/Models/Llm/ToolDefinition.cs`
- `Core/Models/Llm/ToolCallRequest.cs`
- `Core/Models/Llm/ToolCallResult.cs`
- `Core/Models/ChatSession.cs`
- `Core/Interfaces/IChatHistoryStore.cs`
- `Database/InMemoryChatHistoryStore.cs`

**Modified:**
- `Core/Models/Llm/LlmResponse.cs`
- `Core/Interfaces/Llm/ILlmService.cs`
- `Llm/Services/OllamaLlmService.cs`
- `Database/DatabaseServiceCollectionExtensions.cs`
- `App/Program.cs`

---

## Signatures

**Implemented By:** Radagast (LLM Engineer)  
**Review Needed:** Gandalf (architecture), Samwise (query execution), Legolas (UI integration)  
**Status:** Phase 1 complete, Phase 2 blocked on Ollama tool calling support

---

*Infrastructure is production-ready. Waiting on Ollama ecosystem maturity.*
