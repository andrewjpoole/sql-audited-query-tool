# Legolas — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: UI must clearly separate readonly queries from fix suggestions.
- Owns: Chat UI, query interface, results display, user interaction

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.App` — ASP.NET Core Empty (dotnet new web) — minimal, ready for chat UI build
- **Architecture:** App is composition root; references Core, Database, Audit, Llm
- **UI patterns:** Build on ASP.NET Core empty template; endpoints for chat, query execution, results display
- **Ready to start:** Chat UI scaffolding, API endpoint design, results rendering
