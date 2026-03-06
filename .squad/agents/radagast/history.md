# Radagast — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: LLM must NEVER be exposed to actual database data — only schema, query patterns, code structure.
- Owns: Local LLM ops, SQL Server MCP integration, query generation safety

## Learnings
<!-- Append new learnings below this line -->

### 2025-07-18: Qwen 3.5 Thinking Model Support
- **Context:** Andrew switching from `qwen2.5:7b` to `qwen3.5:9b` (and smaller variants) — broke due to thinking mode
- **OllamaSharp 5.4.23 findings:**
  - `ChatRequest.Think` property exists — accepts `ThinkValue` (implicit conversion from `bool`)
  - `Message.Thinking` property separates thinking content from `Content` when Think is enabled
  - `ChatOptions.AddOllamaOption(OllamaOption.Think, value)` works for the M.E.AI streaming path
- **Implementation:**
  - Added `ThinkingEnabled` bool to `OllamaOptions` (default: false — fast mode for small models)
  - Non-streaming: sets `ChatRequest.Think = _options.ThinkingEnabled`, logs if `Message.Thinking` is populated, strips inline `<think>` tags as fallback
  - Streaming: sets `OllamaOption.Think` via `ChatOptions.AddOllamaOption()`, `StreamingThinkingFilter` still handles inline tags as fallback
  - Config in `appsettings.json`: `"ThinkingEnabled": false`
- **Backward compat:** qwen2.5 (non-thinking) unaffected — `Think=false` is a no-op, inline tag stripping still works
- **Key files:** `OllamaOptions.cs`, `OllamaLlmService.cs`, `appsettings.json`


### 2026-03-06: Schema Validation Auto-Retry — LLM Feedback Loop
- **Context:** User directive: validation errors should be auto-corrected by LLM instead of shown to user
- **Implementation:** Samwise added retry loop in `/api/chat` endpoint (both streaming and non-streaming paths)
- **How It Works:** When `SqlSchemaValidator.Validate()` finds warnings:
  1. Build feedback message: "Your suggested query has schema validation issues:\n\n{details}\n\nPlease fix the query and suggest a corrected version."
  2. Add feedback as user message to `llmRequest.Messages`
  3. Call `llmService.ChatAsync(llmRequest, ct)` to get corrected response
  4. Save corrected response to chat history
  5. Repeat up to 2 times max
- **Rationale:** LLM can self-correct when given specific validation feedback. Reduces poor UX of showing raw validation errors.
- **Max Retries:** 2 (hardcoded, can be configurable later)
- **Fail-Safe:** If warnings persist after retries, they attach to `SuggestedQuery.SchemaWarnings` (existing fallback)
- **Message History:** Each retry saves assistant response to preserve conversation flow for debugging
- **Latency Impact:** ~1-3 seconds per retry (6 seconds max for 2 retries)

## Foundation Work Summarization (2026-02-22 to 2026-03-04)

Early work consolidated includes:
- **Schema validation:** `SqlSchemaValidator` with regex-based parsing, Levenshtein fuzzy matching, validation post-LLM in `/api/chat`
- **Code analysis:** Multi-tier hierarchy (ClassAnalysis/PropertySummary for general, EntityDefinition/PropertyDefinition for EF, DapperUsage/AdoNetUsage for audit trail)
- **LLM integration:** Aspire Ollama via CommunityToolkit, OllamaSharp typed API, configurable chat timeouts
- **SSE streaming:** Structured event streaming from `/api/chat` (tool_start, tool_result, text, done)
- **Batch work:** Domain methods on entities, query audit exclusions for failed executions
- **Embeddings strategy:** Local-only processing for completion suggestions (data never leaves infrastructure)
- **Hot reload debugging:** Discovered Aspire watches file changes not new files — must rebuild projects for new services

---
---

### 2026-03-06: SSE Streaming & Thinking Content Pipeline
- **Context:** Response text was not streaming to the frontend — the full response was sent only in the `done` event. Thinking content from qwen3.5 was being silently discarded by the `StreamingThinkingFilter`.
- **Root Cause:** The `/api/chat` streaming path called `ChatAsync` (non-streaming) instead of `StreamChatAsync`. The `StreamingThinkingFilter` stripped `<think>` blocks entirely.
- **Changes:**
  1. **New `StreamChunk` record** (`Core/Models/Llm/StreamChunk.cs`): `record StreamChunk(string Content, bool IsThinking = false)` — distinguishes text from thinking content.
  2. **`ILlmService.StreamChatAsync`** return type changed: `IAsyncEnumerable<string>` → `IAsyncEnumerable<StreamChunk>`.
  3. **`StreamingThinkingFilter`** now yields thinking content as `StreamChunk(IsThinking: true)` instead of discarding it. Regular text yields as `StreamChunk(IsThinking: false)`.
  4. **`/api/chat` streaming endpoint** restructured:
     - Phase 1: Tool-calling loop with `ChatAsync` (tool calls need full response for detection) + SSE `tool_start`/`tool_result` events.
     - Phase 2: `StreamChatAsync` for final text delivery — each chunk emitted as SSE `{ type: "text", content: "..." }` or `{ type: "thinking", content: "..." }` events.
  5. **`ThinkingEnabled` set to `true`** in `appsettings.json` for qwen3.5 thinking support.
  6. **`ParseSuggestedQueries` made `public`** (was `internal`) for use from `Program.cs`.
- **Architecture trade-off:** Tool-calling rounds use `ChatAsync` (non-streaming) because tool calls require the full response for detection. After tools resolve, `StreamChatAsync` re-generates the final response with full conversation context for true streaming delivery. Without tool calls, `ChatAsync` checks for tools, then `StreamChatAsync` delivers the streamed response.
- **SSE event types:** `tool_start`, `tool_result`, `text`, `thinking`, `schema_retry`, `done`
- **Key files:** `StreamChunk.cs`, `ILlmService.cs`, `OllamaLlmService.cs`, `Program.cs`, `appsettings.json`

---

