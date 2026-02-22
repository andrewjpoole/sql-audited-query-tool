# Faramir — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly enforcement, no data exposure to LLM, audit trail integrity.
- Owns: Security review, readonly enforcement, data privacy, compliance

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Architecture review points:**
  - Core: Define readonly query interfaces and constraints (no mutation)
  - Database: Enforce readonly connection strings and query patterns
  - Audit: All queries logged to GitHub issues (immutable audit trail)
  - Llm: Never expose actual data — only schema, patterns, suggestions
  - App: Enforce endpoint authorization and readonly compliance
- **Security checklist:** Review each layer for data isolation, LLM safety, audit integrity
- **Ready to start:** Security architecture review, threat modeling, enforcement patterns
