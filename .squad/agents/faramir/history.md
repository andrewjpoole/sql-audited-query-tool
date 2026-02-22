# Faramir — History

## Core Context
- Project: SQL Audited Query Tool
- User: Andrew
- Stack: .NET / C#, SQL Server, Local LLM, SQL Server MCP, EF Core, Chat UI
- Key constraint: Readonly enforcement, no data exposure to LLM, audit trail integrity.
- Owns: Security review, readonly enforcement, data privacy, compliance

## Learnings
<!-- Append new learnings below this line -->

### 2026-02-22T15:45:00Z: SQL Server MCP Security Assessment — CRITICAL RISK
- **Assessment:** Reviewed security implications of using SQL Server MCP (`@fynet/mcp-mssql`) to give Ollama direct database access
- **Risk Rating:** 🔴 **CRITICAL (9/10)** — REJECT as currently proposed
- **Core Finding:** Direct MCP connectivity **violates** the requirement "*strictly without exposing any data from the database*"
- **Attack Vectors Identified:**
  1. **Prompt Injection → Data Exfiltration** (HIGH likelihood) — malicious prompts can trick LLM into SELECT on data tables
  2. **Tool Misuse** (MEDIUM-HIGH) — LLM may execute data-returning queries to "help" user
  3. **Bypassed Audit Trail** (CERTAIN) — MCP queries don't flow through `GitHubAuditLogger`
  4. **Credential Exposure** (MEDIUM) — MCP config requires full DB credentials in plaintext
  5. **No Defense in Depth** — MCP removes 5 out of 6 existing security controls
- **MCP Capabilities Analysis:**
  - `@fynet/mcp-mssql` exposes full SQL execution (SELECT, INSERT, UPDATE, DELETE)
  - No built-in read-only mode or schema-only filtering
  - LLM autonomously invokes tools based on natural language — no granular access control
  - Protocol assumes trusted LLM agent — incompatible with our adversarial threat model
- **Current Architecture (SchemaMetadataProvider) — SECURE:**
  - ✅ Controlled surface (only INFORMATION_SCHEMA/sys.* views)
  - ✅ Static hardcoded queries (no LLM influence)
  - ✅ Cached schema (minimal DB interaction)
  - ✅ Payload validation (`DataLeakPrevention` scans before LLM)
  - ✅ Complete audit trail (all queries logged to GitHub)
  - ✅ Readonly connections enforced
- **Recommendation:** **REJECT** direct SQL Server MCP. Current `SchemaMetadataProvider` architecture is **SECURE and COMPLIANT**.
- **Conditional Approval Path:** If MCP is required, build custom schema-only MCP server with:
  - Only `get_schema` tool (no query execution)
  - Hardcoded INFORMATION_SCHEMA queries
  - Integrated `DataLeakPrevention` validation
  - Schema-only database replica (no data tables)
  - 2-3 weeks engineering effort + ongoing maintenance
  - Risk after mitigations: 🟡 MEDIUM (5/10)
- **Next Steps:** Clarify requirement with Andrew — is direct MCP truly needed, or is current schema-only LLM assistance sufficient?
- **Decision Document:** `.squad/decisions/inbox/faramir-mcp-security-assessment.md`

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

### 2026-02-22T14:20:00Z: Security Middleware & Validation Implemented
- **SqlValidator** (`Core/Security/SqlValidator.cs`):
  - `ValidateReadOnly()` — regex-based detection that strips string literals and comments before scanning for 13 blocked keywords (INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE, GRANT, REVOKE, DENY) plus sp_/xp_ prefixes
  - `SanitizeForAudit()` — redacts password/token/key/secret patterns before audit logging
  - `ValidationResult` model with IsValid, Violations list, RiskLevel enum (Safe/Suspicious/Blocked)
  - UNION flagged as Suspicious (not Blocked) — allows legitimate UNION SELECTs while alerting reviewers
  - Multi-statement batches (semicolons) flagged as Suspicious
- **DataLeakPrevention** (`Core/Security/DataLeakPrevention.cs`):
  - `ValidateLlmPayload()` / `InspectPayload()` — scans serialized payloads for PII (email, SSN, phone, credit card, GUIDs) and large string arrays that look like row data
  - Structural JSON scanning handles nested objects
- **AuditIntegrity** (`Core/Security/AuditIntegrity.cs`):
  - `GenerateAuditHash()` — SHA-256 over canonical payload of request + result metadata
  - `VerifyAuditHash()` — tamper detection for audit entries
- **Models** (`Core/Models/`): QueryRequest, QueryResult, AuditEntry
- **Tests:** 58 tests in Core.Tests/Security/ — all passing. Covers write blocking, comment/string-literal edge cases, UNION injection, multi-statement, PII detection, row data arrays, hash determinism, tamper detection, null guards.
- **Security design decisions:** See `.squad/decisions/inbox/faramir-security-contracts.md`
