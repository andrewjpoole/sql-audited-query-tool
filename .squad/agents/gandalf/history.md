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

### 2026-02-22: SQL Server MCP Architecture Decision
- **Question:** Should we use SQL Server MCP to give Ollama direct database access?
- **Decision:** **NO — keep current architecture**
- **Rationale:**
  - Core requirement: *"strictly without exposing any data from the database"*
  - Current design enforces data isolation at architectural level (LLM has no DB connection)
  - SchemaMetadataProvider already provides schema context to LLM
  - MCP would add complexity without functional benefit for our use case
  - Ollama doesn't natively support MCP tool calling — would need adapter
- **MCP Research:**
  - C# MCP server (`aadversteeg/mssqlclient-mcp-server`) has query execution disabled by default
  - Python MCP server (`RichardHan/mssql_mcp_server`) always enables data access — NOT safe for our use case
  - Even schema-only MCP adds attack surface without adding value over SchemaMetadataProvider
- **Architecture Preserved:**
  - `SchemaMetadataProvider` → schema context → Ollama (no data exposure)
  - User executes queries via `QueryExecutor` (readonly)
  - All queries audited to GitHub issues via `AuditService`
- **Security Principle:** Air gap between LLM and database — data leakage is architecturally impossible

### 2026-02-22: MCP Decision REVISED — Local LLM Can Access Data
- **Trigger:** Andrew clarified: *"the 'strictly without exposing any data' requirement doesn't apply to locally running ollama models"*
- **Key Insight:** The original security constraint was about preventing data leakage to **external services**, not local processing. Since Ollama runs locally, data never leaves the infrastructure.
- **New Decision:** **YES — Implement App-Orchestrated Tool Calling**
- **Architecture Change:**
  - Add Ollama tool calling (execute_query, get_schema)
  - Our app orchestrates MCP-style tool calls via existing QueryExecutor
  - Results fed back to Ollama for analysis
  - All queries still flow through AuditService → GitHub issues
- **Why Not External MCP Bridge:**
  - External bridge might bypass our audit trail
  - App-orchestrated approach gives us full control
  - Simpler deployment (no Node.js dependency)
- **Benefits Unlocked:**
  - Ollama can see actual query results
  - Iterative investigation (query → analyze → refine)
  - Much better incident investigation assistance
- **Security Preserved:**
  - Readonly enforcement via QueryExecutor
  - Complete audit trail to GitHub issues
  - Data stays local (Ollama runs on-prem)
