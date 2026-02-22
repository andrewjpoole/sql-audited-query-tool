# Samwise — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly database access only. All queries audited to GitHub issues.
- Owns: DB access layer, API services, audit logging, EF Core discovery

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.Database` — will handle SQL Server readonly access, query execution, EF Core integration
- **Core reference:** All src projects reference Core (no circular deps)
- **App composition:** App references Database along with Audit and Llm
- **Test project:** `SqlAuditedQueryTool.Database.Tests` with xUnit — ready for EF Core and query layer tests
- **Ready to start:** Database layer implementation, connection patterns, EF Core DbContext setup
