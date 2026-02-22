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

### 2025-07-24: Project Structure Established
- **Solution:** `SqlAuditedQueryTool.sln` at repo root
- **Target Framework:** net9.0 (using .NET 10 SDK)
- **App type:** ASP.NET Core Empty (`dotnet new web`) for SqlAuditedQueryTool.App — chosen to support chat UI
- **Layered architecture:** Core → Database/Audit/Llm → App (dependency flows inward)
- **Reference graph:**
  - Core is referenced by Database, Audit, Llm, and App
  - App references all four src projects
  - Each test project references its corresponding src project
- **Test framework:** xUnit (default `dotnet new xunit`)
- **Key paths:**
  - Solution: `SqlAuditedQueryTool.sln`
  - Source: `src/SqlAuditedQueryTool.{Core,Database,Audit,Llm,App}/`
  - Tests: `tests/SqlAuditedQueryTool.{Core,Database,Audit,Llm}.Tests/`
- **Andrew preference:** .NET 9.0 LTS target
