# Radagast — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: LLM must NEVER be exposed to actual database data — only schema, query patterns, code structure.
- Owns: Local LLM ops, SQL Server MCP integration, query generation safety

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T12:18:00Z: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root, targeting net9.0
- **Your project:** `SqlAuditedQueryTool.Llm` — will handle local LLM integration and SQL Server MCP client
- **Safety pattern:** Core and Database layers provide schema/pattern contracts; Llm consumes these safely without raw data
- **App composition:** App references Llm along with Database and Audit
- **Test project:** `SqlAuditedQueryTool.Llm.Tests` with xUnit — ready for LLM safety and MCP integration tests
- **Ready to start:** LLM initialization, MCP client setup, prompt safety validation
