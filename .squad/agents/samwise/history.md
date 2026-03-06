# Samwise — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly database access only. All queries audited to GitHub issues.
- Owns: DB access layer, API services, audit logging, EF Core discovery

## Learnings
<!-- Append new learnings below this line -->

### 2026-03-06: SQL Comment Stripping to Fix False Validation & Write-Query Detection
- **Bug:** SQL comments (e.g., `-- Update the Account...`) caused false schema validation warnings and prevented write-query detection when comments preceded the actual statement.
- **Fix:** Created `SqlHelper.StripSqlComments()` in `src\SqlAuditedQueryTool.Llm\Services\SqlHelper.cs` — uses `GeneratedRegex` to strip `--` and `/* */` comments while preserving string literals.
- **Applied in 3 places:**
  - `SqlSchemaValidator.Validate()` — strips comments before `ExtractTableReferences` and `ExtractColumnReferences`
  - `OllamaLlmService.IsFixQuery()` — strips comments before `StartsWith` checks
  - `LlmQueryAssistant` line 73 — strips comments before `StartsWith` checks
- **Pattern:** Comment stripping is shared via static helper to keep DRY. Original SQL preserved for display; only validation/detection uses stripped version.

### 2026-02-22: Schema Validation Retry Loop for LLM-Generated Queries
- **Feature:** Modified `/api/chat` endpoint (streaming and non-streaming paths) to automatically retry schema validation failures with the LLM before presenting warnings to the user.
- **Implementation:**
  - After LLM responds with suggested queries, validate each using `SqlSchemaValidator.Validate(sql, schema)` 
  - If warnings found, construct feedback message: "Your suggested query has schema validation issues: {warnings}. Please fix the query and suggest a corrected version."
  - Send feedback back to LLM via `llmService.ChatAsync()` with the warnings
  - Retry up to 2 times maximum
  - On streaming path: send `schema_retry` SSE event so frontend can show "🔄 Fixing schema issues..." status
  - If still warnings after max retries, attach them to `SuggestedQuery.SchemaWarnings` as before (frontend fallback still works)
- **Key files modified:**
  - `src\SqlAuditedQueryTool.App\Program.cs` — lines ~374-444 (streaming), ~570-630 (non-streaming)
  - `src\SqlAuditedQueryTool.App\ClientApp\src\api\queryApi.ts` — added `schema_retry` to `StreamEvent` type
  - `src\SqlAuditedQueryTool.App\ClientApp\src\components\ChatPanel.tsx` — handle `schema_retry` event with status message
- **Retry logic pattern:** Loop with `hasWarnings` check, early-exit if no warnings, build feedback from all queries with warnings, append user message with feedback, call LLM again, save response to chat history
- **UX improvement:** Users see in-progress retry status instead of raw warnings, improving perceived intelligence of the system
- **Backward compatible:** Remaining warnings after retries still flow to frontend via existing `schemaWarnings` field — SuggestionCard already handles them


### 2025-07-24: SQL Comment Stripping to Fix False Validation & ReadOnly Detection
- **Bug:** SQL comments (e.g., `-- Update the Account...`) caused false schema validation warnings and prevented write-query detection when comments preceded the actual statement.
- **Fix:** Created `SqlHelper.StripSqlComments()` in `src\SqlAuditedQueryTool.Llm\Services\SqlHelper.cs` — uses `GeneratedRegex` to strip `--` and `/* */` comments while preserving string literals.
- **Applied in 3 places:**
  - `SqlSchemaValidator.Validate()` — strips comments before `ExtractTableReferences` and `ExtractColumnReferences`
  - `OllamaLlmService.IsFixQuery()` — strips comments before `StartsWith` checks
  - `LlmQueryAssistant` line 73 — strips comments before `StartsWith` checks
- **Pattern:** Comment stripping is shared via static helper to keep DRY. Original SQL preserved for display; only validation/detection uses stripped version.

## Foundation Work Summarization

Early foundational work includes project structure, core database/API features, LLM integration, audit logging, and architectural decisions. Details available in git history.
