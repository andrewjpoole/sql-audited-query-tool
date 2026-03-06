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
### 2026-02-23: Code Review — Clean Codebase with Minimal Cleanup Needed
- **Task:** Thorough code review of entire `src/` directory (.NET 9 ASP.NET Core + React/TypeScript)
- **Findings Summary:** Codebase is exceptionally clean. Only 2 TODOs, minimal debug logging (intentional configuration/diagnostic), no dead code, no unused classes.
- **Key Observations:**
  - **Debug Logging (LOW):** Console.log statements in App.tsx (lines 77-84, 129-136) and vite.config.ts (lines 9-11) are intentional diagnostic logging for result set tracking and Aspire proxy configuration — NOT debug code left behind. One console.error in ExecutionPlanView.tsx (line 21) for clipboard error handling — appropriate.
  - **TODO Comments (MEDIUM):** Only 2 TODOs found:
    1. `Program.cs:430` — Replace hardcoded "anonymous" user with authenticated user (blocked on auth implementation)
    2. `OllamaLlmService.cs:643` — Commented-out `BuildTools()` method for future Ollama tool calling support (infrastructure ready, awaiting Ollama update)
  - **Unused Code (MEDIUM):** Three unused classes in Core.Security namespace:
    1. `DataLeakPrevention.cs` — PII detection and payload validation for LLM (schema-only enforcement) — **designed but not yet wired**
    2. `AuditIntegrity.cs` — SHA-256 audit hash generation/verification — **designed but GitHubAuditLogger uses simplified hash**
    3. `SchemaEmbeddingService.cs` — Background service for embedding schema metadata — **never registered in DI**
  - **Commented Code Block (LOW):** OllamaLlmService.cs lines 644-655 — Placeholder for Ollama native tool calling (waiting for upstream support). Infrastructure exists via `BuildTools()` method on line 228.
  - **No Other Issues:** No unreachable code, no unused imports, no swallowed exceptions, no magic strings requiring constants, no copy-paste code needing refactoring.
- **Architecture Quality:** Clean separation of concerns, proper DI scoping, comprehensive interfaces, good logging practices.
- **Security Posture:** Three security classes exist but aren't enforced yet. `DataLeakPrevention` and `AuditIntegrity` should be integrated before production.
- **Recommendation:** Wire security classes before production, address TODOs when auth is implemented, keep diagnostic logging as-is (valuable for debugging).


## Foundation Work Summarization

Early foundational work includes project structure, core database/API features, LLM integration, audit logging, and architectural decisions. Details available in git history.
