# Decisions

<!-- Append-only. Scribe merges from decisions/inbox/. -->

### 2026-02-22T12:07:00Z: Team formed
**By:** Squad (Coordinator)
**What:** Team hired for SQL Audited Query Tool project. Members: Gandalf (Lead), Samwise (Backend), Radagast (LLM Engineer), Legolas (Frontend), Faramir (Security).
**Why:** Project kickoff — Andrew's initial request.

### 2026-02-22T12:18:00Z: Project Structure — Gandalf
**By:** Gandalf (Lead)
**What:** Established solution structure with 5 src projects and 4 test projects targeting net9.0.
**Layout:**
- `SqlAuditedQueryTool.Core` — domain models, interfaces, shared types (referenced by all)
- `SqlAuditedQueryTool.Database` — SQL Server readonly access, query execution, EF Core
- `SqlAuditedQueryTool.Audit` — GitHub issue audit logging
- `SqlAuditedQueryTool.Llm` — local LLM integration, SQL Server MCP client
- `SqlAuditedQueryTool.App` — ASP.NET Core web app (chat UI host)
- 4 xUnit test projects mirror the src layer
**Why:** Clean separation of concerns — each domain responsibility is isolated, testable, and independently deployable. Core sits at the center with no outward dependencies. App is the composition root.
**Constraints:**
- net9.0 target framework (LTS)
- ASP.NET Core Empty template for App (minimal, no MVC scaffolding — will build chat UI on top)
- xUnit as test framework
