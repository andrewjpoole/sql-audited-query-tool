# Radagast — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: LLM must NEVER be exposed to actual database data — only schema, query patterns, code structure.
- Owns: Local LLM ops, SQL Server MCP integration, query generation safety

## Learnings
<!-- Append new learnings below this line -->

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


