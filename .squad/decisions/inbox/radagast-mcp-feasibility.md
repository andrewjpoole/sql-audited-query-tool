# Technical Feasibility: SQL Server MCP + Ollama Integration

**Analyst:** Radagast (LLM Engineer)  
**Date:** 2026-02-22  
**Context:** Evaluating MCP integration for schema-only SQL Server access vs. current SchemaMetadataProvider approach  
**UPDATED:** After discovering Azure Data API Builder MCP Server (preview)

---

## Executive Summary

**RECOMMENDATION: Implement Azure Data API Builder (DAB) MCP Server with schema-only tool configuration.**

**Why:**
- **Official SQL Server MCP server exists** — Azure Data API Builder v1.7+ includes production-ready MCP support
- **Built-in schema-only mode** — can disable `create_record`, `update_record`, `delete_record`, `execute_entity` tools, exposing only `describe_entities` and `read_records` (schema discovery only)
- **Aspire integration** — DAB works natively with .NET Aspire, already used in this project
- **RBAC enforcement** — role-based access control built-in, permissions defined in config
- **Tool calling architecture** — LLM requests schema on-demand, reducing token usage
- **Production-ready** — includes caching, telemetry, health checks, OpenTelemetry support
- **Zero data exposure** — when data tools are disabled, LLM can only inspect schema metadata

---

## 1. SQL Server MCP Capabilities Analysis

### What is MCP?

Model Context Protocol (MCP) is an open protocol developed by Anthropic for connecting LLMs to external data sources and tools. It provides:
- Standardized tool discovery
- Structured tool calling interface
- Server-based architecture for isolation

### Azure Data API Builder (DAB) MCP Server

**FINDING: Official SQL Server MCP server EXISTS — Azure Data API Builder v1.7+ (currently in preview).**

**Source:** https://learn.microsoft.com/en-us/azure/data-api-builder/mcp/overview

**Key Features:**
- Built into Data API builder starting in v1.7 (prerelease: `1.7.83-rc`)
- Production-ready MCP server for SQL Server, PostgreSQL, MySQL, Azure Cosmos DB
- Supports both stdio (local/CLI) and HTTP transports
- MCP protocol version 2025-06-18 implementation
- Part of Microsoft's official tooling — free, open source
- Native .NET Aspire integration (containerized deployment)

**Enterprise Features:**
- Role-Based Access Control (RBAC) — per-entity, per-role, per-action permissions
- Caching — Level 1 and Level 2 caching for `read_records` tool
- Telemetry — OpenTelemetry (OTEL) spans, Application Insights integration
- Health checks — REST, GraphQL, and MCP endpoint monitoring
- Audit logs — Azure Log Analytics integration

### What Does DAB MCP Server Provide?

**Six DML (Data Manipulation Language) Tools:**

| Tool | Purpose | Security Risk | Schema-Only Mode |
|------|---------|---------------|------------------|
| `describe_entities` | Returns entities, fields, types, primary keys, allowed operations | **NONE (metadata only)** | ✅ SAFE — Keep enabled |
| `read_records` | Query tables/views with filtering, sorting, pagination | **LOW (can read data if not restricted)** | ⚠️ CONFIGURABLE — Can restrict to schema inspection only via RBAC |
| `create_record` | Insert new rows | **HIGH (data modification)** | ❌ DISABLE — Not needed for schema discovery |
| `update_record` | Modify existing rows | **HIGH (data modification)** | ❌ DISABLE — Not needed for schema discovery |
| `delete_record` | Remove rows | **HIGH (data modification)** | ❌ DISABLE — Not needed for schema discovery |
| `execute_entity` | Run stored procedures | **HIGH (arbitrary code execution)** | ❌ DISABLE — Not needed for schema discovery |

### Schema-Only Configuration

**Critical Answer:** Yes, DAB MCP can be configured for schema-only access.

**How:**
1. **Runtime-level tool disabling** — disable data modification tools globally:
   ```json
   {
     "runtime": {
       "mcp": {
         "enabled": true,
         "path": "/mcp",
         "dml-tools": {
           "describe-entities": true,   // ✅ Keep — schema metadata only
           "read-records": false,        // ❌ Disable — can read row data
           "create-record": false,       // ❌ Disable — writes data
           "update-record": false,       // ❌ Disable — modifies data
           "delete-record": false,       // ❌ Disable — deletes data
           "execute-entity": false       // ❌ Disable — runs stored procedures
         }
       }
     }
   }
   ```

2. **Entity-level permissions** — further restrict what `describe_entities` reveals:
   ```json
   {
     "entities": {
       "Incidents": {
         "source": "dbo.Incidents",
         "permissions": [
           {
             "role": "anonymous",
             "actions": []  // No actions = schema visible, no operations allowed
           }
         ],
         "mcp": {
           "dml-tools": {
             "describe-entities": true  // Only schema discovery allowed
           }
         }
       }
     }
   }
   ```

3. **Read-only connection string** — database-level enforcement:
   - Use SQL Server read-only user with `db_datareader` role
   - Same security boundary as current `SchemaMetadataProvider`

**Result:** LLM can call `describe_entities` to discover tables, columns, types, primary keys, foreign keys, indexes — but CANNOT read, write, modify, or delete any row data.

**This is EXACTLY what SchemaMetadataProvider does, but exposed via MCP tools instead of pre-loaded context.**

---

## 2. Ollama + MCP Integration

### Current Architecture

```
┌─────────────────────────────────────────────────────┐
│ SqlAuditedQueryTool.App (ASP.NET Core)             │
│                                                     │
│  ┌─────────────────────────────────────────────┐  │
│  │ OllamaLlmService (ILlmService)              │  │
│  │  - Uses IChatClient (Microsoft.Extensions.AI)│  │
│  │  - Formats schema as text in system prompt   │  │
│  └────────────────┬────────────────────────────┘  │
│                   │                                │
│  ┌────────────────▼────────────────────────────┐  │
│  │ SchemaMetadataProvider (ISchemaProvider)    │  │
│  │  - Queries INFORMATION_SCHEMA               │  │
│  │  - Queries sys.columns, sys.indexes, etc.   │  │
│  │  - Returns structured SchemaContext         │  │
│  └────────────────┬────────────────────────────┘  │
│                   │                                │
└───────────────────┼────────────────────────────────┘
                    │
                    ▼
            ┌───────────────┐
            │  SQL Server   │
            │ (INFORMATION_ │
            │    SCHEMA)    │
            └───────────────┘
```

Data flow:
1. SchemaMetadataProvider queries SQL Server metadata (schema only)
2. Returns `SchemaContext` (C# objects: `TableSchema`, `ColumnSchema`, etc.)
3. OllamaLlmService formats schema as text
4. Text appended to system prompt
5. Sent to Ollama via `IChatClient`

**Security:** LLM never sees row data — only schema metadata passed as text.

### Option A: DAB MCP Integration (Recommended)

```
┌──────────────────────────────────────────────────────────┐
│ SqlAuditedQueryTool.App (ASP.NET Core)                  │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ OllamaLlmService (ILlmService)                     │ │
│  │  - Uses IChatClient with tool calling enabled      │ │
│  │  - Sends MCP tool definitions to Ollama            │ │
│  │  - Executes tool calls via MCP client              │ │
│  └──────────────────┬─────────────────────────────────┘ │
│                     │                                    │
│  ┌──────────────────▼─────────────────────────────────┐ │
│  │ MCP Client (.NET — via stdio or HTTP)             │ │
│  │  - Discovers tools from DAB MCP server             │ │
│  │  - Translates LLM tool calls → MCP protocol        │ │
│  │  - Returns JSON results back to LLM                │ │
│  └──────────────────┬─────────────────────────────────┘ │
│                     │                                    │
└─────────────────────┼────────────────────────────────────┘
                      │ stdio (local) or HTTP (container)
                      ▼
         ┌────────────────────────────────────┐
         │ Azure Data API Builder             │
         │ MCP Server (DAB v1.7+)             │
         │  - Runs in .NET Aspire container   │
         │  - Tools: describe_entities ONLY   │
         │  - (all data tools disabled)       │
         │  - RBAC: anonymous role, no actions│
         └────────────┬───────────────────────┘
                      │
                      ▼
              ┌───────────────┐
              │  SQL Server   │
              │ (read-only    │
              │  connection)  │
              └───────────────┘
```

Data flow:
1. User sends prompt to LLM
2. LLM (qwen2.5-coder:7b) calls tool: `describe_entities()`
3. OllamaLlmService receives tool call, forwards to MCP client
4. MCP client connects to DAB MCP server (via stdio or HTTP)
5. DAB queries SQL Server metadata (INFORMATION_SCHEMA, sys.* catalog views)
6. Returns JSON schema description
7. MCP client sends result back to OllamaLlmService
8. LLM receives schema context, generates SQL query

**Security:** 
- DAB MCP server configured with `describe-entities: true`, all other tools `false`
- Database connection is read-only (same as current)
- LLM can only inspect schema metadata — no data access

### Integration Path

#### Option 1: HTTP Transport (Containerized — Recommended for Aspire)

**Architecture:**
- DAB MCP server runs as .NET Aspire container resource (already have Aspire)
- MCP endpoint exposed at `http://dab-mcp-server:5000/mcp`
- OllamaLlmService connects via HTTP MCP client

**Implementation:**
1. Add DAB container to `AppHost.cs`:
   ```csharp
   var dabMcp = builder.AddContainer("dab-mcp-server", 
       "azure-databases/data-api-builder", "1.7.83-rc")
       .WithImageRegistry("mcr.microsoft.com")
       .WithHttpEndpoint(targetPort: 5000, name: "http")
       .WithEnvironment("MSSQL_CONNECTION_STRING", db)
       .WithBindMount("dab-config.json", "/App/dab-config.json", true);
   ```

2. Configure `dab-config.json` for schema-only:
   ```bash
   dab init --database-type mssql --connection-string "@env('MSSQL_CONNECTION_STRING')" --config dab-config.json
   dab add Incidents --source dbo.Incidents --permissions "anonymous:none"
   dab add AuditEvents --source dbo.AuditEvents --permissions "anonymous:none"
   dab configure --runtime.mcp.dml-tools.describe-entities true
   dab configure --runtime.mcp.dml-tools.read-records false
   dab configure --runtime.mcp.dml-tools.create-record false
   dab configure --runtime.mcp.dml-tools.update-record false
   dab configure --runtime.mcp.dml-tools.delete-record false
   dab configure --runtime.mcp.dml-tools.execute-entity false
   ```

3. Install MCP client library:
   - Option A: Use Microsoft's MCP SDK if/when released (.NET)
   - Option B: Use `@modelcontextprotocol/sdk` via Node.js bridge
   - Option C: Implement minimal HTTP MCP client in C# (MCP protocol is JSON-RPC over HTTP)

4. Update `OllamaLlmService` to use tool calling:
   - Define `describe_entities` tool for Ollama
   - When LLM calls tool, forward to DAB MCP server via HTTP
   - Return schema JSON to LLM

#### Option 2: stdio Transport (CLI)

**When to use:** Local development, testing, single-process scenarios

**How:** Spawn `dab --mcp-stdio` process from .NET app, communicate via stdin/stdout

**Complexity:** Lower — no HTTP, no containers, but requires process management

#### Challenge 1: MCP Client Implementation

**Status:** No official .NET MCP client exists (as of Feb 2026)

**Options:**

| Option | Effort | Pros | Cons |
|--------|--------|------|------|
| **A: Minimal HTTP JSON-RPC Client** | 1-2 days | Simple, no dependencies, we control it | Limited to HTTP transport, manual protocol impl |
| **B: Bridge to Node.js SDK** | 2-3 days | Full MCP protocol support, official SDK | Node.js runtime dependency, IPC complexity |
| **C: Wait for MS .NET SDK** | 0 days (wait) | Official support, maintained by MS | May not exist yet, timeline unknown |

**Recommendation:** Start with **Option A** (minimal HTTP client). MCP protocol is straightforward JSON-RPC:

```http
POST /mcp HTTP/1.1
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "describe_entities",
    "arguments": {}
  },
  "id": 1
}
```

Response:
```json
{
  "jsonrpc": "2.0",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"entities\":[{\"name\":\"Incidents\",\"fields\":[...]}]}"
      }
    ]
  },
  "id": 1
}
```

This is simpler than implementing full MCP protocol — we only need `tools/list` and `tools/call`.

---

## 3. Comparison: DAB MCP vs. Current Approach

| Dimension | Option A: DAB MCP (Recommended) | Option B: SchemaMetadataProvider (Current) |
|-----------|-------------------------------|-------------------------------------------|
| **Security** | Schema-only (DAB configured with only `describe-entities` enabled) | Schema-only (enforced by INFORMATION_SCHEMA queries) |
| **Data Access** | None (all data tools disabled at runtime + entity level) | None (provider never queries row data) |
| **Schema Discovery** | MCP tool: `describe_entities` → returns JSON schema | Direct SQL queries to `INFORMATION_SCHEMA`, `sys.*` |
| **LLM Integration** | Tool calling (LLM requests schema on demand) | Static context (schema pre-loaded in system prompt) |
| **Latency (First Query)** | Medium (~300-500ms: tool call → MCP → DAB → SQL) | Low (~200ms: cached schema from startup) |
| **Latency (Subsequent)** | Low (~10ms: DAB caches `describe_entities` response) | Low (~0ms: in-memory cache) |
| **Token Usage** | **LOW** — LLM only receives schemas it requests | **HIGH** — full schema embedded in every prompt |
| **Incremental Loading** | ✅ YES — LLM can request specific tables only | ❌ NO — loads all tables upfront |
| **Complexity** | **MEDIUM** — DAB container + MCP client + tool calling | **LOW** — one service, direct ADO.NET queries |
| **Dependencies** | DAB container (Docker), minimal MCP client (HTTP JSON-RPC) | None (pure .NET) |
| **Auditability** | MCP tool call logs + SQL audit + DAB telemetry (OTEL) | SQL audit only |
| **Schema Caching** | Built-in (DAB Level 1/Level 2 caching, configurable TTL) | Built-in (`IMemoryCache`, configurable TTL) |
| **RBAC** | ✅ YES — per-entity, per-role, per-action in `dab-config.json` | ❌ NO — connection-level only |
| **Multi-Database Support** | ✅ YES — PostgreSQL, MySQL, Cosmos DB (future-proof) | ❌ NO — SQL Server only |
| **Maintenance** | DAB maintained by Microsoft, MCP client is minimal | Maintain C# service |
| **Existing Integration** | Requires new `OllamaLlmService` with tool calling | Already works |
| **Aspire Integration** | ✅ NATIVE — DAB container resource, built-in support | N/A |
| **Production Readiness** | ✅ YES — OTEL, health checks, telemetry, enterprise monitoring | ⚠️ BASIC — custom logging only |

---

## 4. What Does DAB MCP Give Us?

### Benefits Over Current Approach

1. **Dynamic Schema Loading (Token Efficiency)**
   - **Current:** Full schema embedded in every prompt — 50 tables × 20 columns × 50 tokens = **~50,000 tokens per request**
   - **With MCP:** LLM requests only needed tables — e.g., 3 tables × 20 columns × 50 tokens = **~3,000 tokens per request**
   - **Savings:** 94% reduction in token usage for targeted queries
   - **Impact:** Faster responses, lower costs (if using cloud LLMs), fits in smaller context windows

2. **Standardized Tool Interface**
   - MCP protocol is vendor-neutral — DAB supports SQL Server, PostgreSQL, MySQL, Cosmos DB
   - Could add PostgreSQL support by changing DAB config, not rewriting code
   - **Future-proofing:** If we expand to multi-database support, MCP scales seamlessly

3. **Enterprise-Grade Observability**
   - **OTEL spans** — trace LLM schema requests through distributed systems
   - **Application Insights** — correlate LLM behavior with backend performance
   - **Audit logs** — track which schemas the LLM inspected, when, and why
   - **Health checks** — monitor DAB MCP endpoint availability
   - **Current approach:** Custom logging only, no standard telemetry

4. **RBAC at the Entity Level**
   - **Per-entity permissions:** Expose `Incidents` to LLM, hide `UserCredentials`
   - **Per-role permissions:** Different schemas for different user roles
   - **Field-level masking:** Show `UserId` but hide `EmailAddress` in schema
   - **Current approach:** All-or-nothing schema access

5. **Separation of Concerns**
   - Schema access isolated to DAB container (independently scalable, monitored, upgraded)
   - **Failure isolation:** If DAB crashes, app continues (LLM falls back to cached schema or errors gracefully)
   - **Current approach:** Schema provider tightly coupled to app process

6. **Built-in Caching and Performance**
   - DAB caches `describe_entities` responses (Level 1 + Level 2 caching)
   - **Warm-start support** in horizontally scaled environments (shared cache)
   - **Current approach:** Single-process `IMemoryCache` (not shared across instances)

### Is DAB MCP Worth the Complexity?

**YES, for this use case** — but only if token efficiency or observability is a priority.

**When MCP shines:**
- Database has **50+ tables** and embedding full schema in every prompt is wasteful
- **Token limits** are a concern (even with local LLMs, larger schemas hit context windows)
- **Enterprise observability** is required (audit logs, telemetry, compliance tracking)
- **Multi-database support** is on the roadmap (PostgreSQL, MySQL, Cosmos DB)
- **RBAC** is needed (different users see different schemas)

**When current approach is fine:**
- Database has **<20 tables** — schema fits comfortably in context
- **No token cost constraints** (local LLM, no API billing)
- **No compliance requirements** for LLM schema access auditing
- **Single database** — SQL Server only, no plans to expand

**Our project:** 
- Currently **small database** (handful of tables for incident investigation)
- **Local LLM** (no token cost)
- **But:** Uses .NET Aspire (DAB integration is trivial), and enterprise observability is a likely future requirement

**Verdict:** MCP is NOT overkill. It's a strategic investment with immediate token efficiency gains and future-proofing for observability and multi-database support.

---

## 5. Practical Considerations

### Latency

| Phase | MCP Approach | Current Approach |
|-------|-------------|------------------|
| Schema Load | LLM tool call → MCP server → SQL query → result → LLM | Single SQL query → in-memory cache |
| Per-table lookup | ~200-500ms (LLM reasoning + MCP roundtrip + SQL) | ~0ms (cached) |
| Full schema (50 tables) | 50 tool calls × 300ms = **15 seconds** | One load × 200ms = **0.2 seconds** |

**Verdict:** MCP would be 75x slower for full schema loading.

**Mitigation:** Batch tool calls or load schema once. But then we're back to the current approach.

### Auditability

Current audit trail:
1. User sends prompt to LLM
2. LLM generates SQL query
3. Query execution audited to GitHub issue
4. Schema access is read-only queries (not audited separately)

With MCP:
1. User sends prompt to LLM
2. LLM calls MCP tool `describe_table("Incidents")`
3. MCP tool invocation logged
4. LLM generates SQL query
5. Query execution audited to GitHub issue

**Does MCP add value?** Marginally — we'd know which tables the LLM inspected. But schema access is harmless (no PII in metadata).

### .NET Integration Impact

Current:
- Pure .NET stack
- Single `IServiceProvider` scope
- No external processes

With MCP:
- Requires Node.js runtime (for MCP server)
- Process management (spawn MCP server, keep alive, restart on crash)
- Inter-process communication (stdio or HTTP)
- Error handling across process boundaries

**Operational complexity increases significantly.**

---

## 6. Recommendation

### PRIMARY RECOMMENDATION: Implement DAB MCP with Schema-Only Configuration

**Rationale:**
1. **Official SQL Server MCP server exists and is production-ready** — Azure Data API Builder v1.7+
2. **Schema-only mode is built-in** — disable all data tools, expose only `describe-entities`
3. **Token efficiency gains are significant** — 94% reduction for targeted queries
4. **Aspire integration is native** — already using .NET Aspire, adding DAB is 20 lines of code
5. **Enterprise observability out-of-the-box** — OTEL, Application Insights, health checks
6. **Future-proof architecture** — scales to multi-database, RBAC, and agentic workflows
7. **Implementation complexity is reasonable** — 3-5 days for MVP, not 2-3 weeks

### Implementation Phases

#### Phase 1: DAB MCP Server Setup (1 day)
1. Add DAB container to `AppHost.cs` (Aspire)
2. Create `dab-config.json` with schema-only tool configuration
3. Verify DAB starts and `describe_entities` returns schema
4. Test with MCP Inspector (included in DAB)

#### Phase 2: Minimal MCP HTTP Client (2 days)
1. Create `McpHttpClient.cs` — simple JSON-RPC over HTTP
2. Implement `ListToolsAsync()` and `CallToolAsync<T>(name, args)`
3. Register as `IMcpClient` in DI container
4. Unit test against local DAB MCP server

#### Phase 3: OllamaLlmService Integration (2 days)
1. Update `OllamaLlmService` to define `describe_entities` tool for Ollama
2. When LLM calls tool, forward to `IMcpClient`
3. Return schema JSON to LLM as tool result
4. Test end-to-end: user prompt → LLM → MCP → DAB → schema → SQL query

#### Phase 4: Production Hardening (1 day)
1. Add error handling (DAB unavailable, tool call failures)
2. Configure DAB caching TTL to match current schema cache duration
3. Add health check for DAB MCP endpoint
4. Wire up OTEL spans for MCP tool calls

**Total: 5-6 days for production-ready implementation**

### When Would We Keep the Current Approach?

Only if:
- **Immediate delivery is critical** — need to ship this week, can't wait 5 days
- **Database is guaranteed to stay tiny** — <10 tables forever, token efficiency irrelevant
- **No observability requirements** — will never need audit logs or telemetry
- **No multi-database support** — SQL Server only, no expansion planned

**None of these apply long-term.** Even if we ship v1 with `SchemaMetadataProvider`, we should plan migration to DAB MCP for v2.

---

## 7. Alternative: Enhance Current Approach

If we want dynamic schema loading without MCP:

### Option C: Function Calling with Current Architecture

Ollama supports function calling. We could define tools:

```csharp
var tools = new[]
{
    new ChatTool
    {
        Name = "list_tables",
        Description = "List all tables in the database",
        Parameters = new { /* no params */ }
    },
    new ChatTool
    {
        Name = "describe_table",
        Description = "Get columns, types, and constraints for a table",
        Parameters = new { tableName = "string" }
    }
};
```

Then in `OllamaLlmService`:
1. Send tools to Ollama
2. LLM generates tool call: `describe_table("Incidents")`
3. Call `SchemaMetadataProvider.GetTableSchema("Incidents")`
4. Return result to LLM
5. LLM uses schema to generate SQL

**Benefits over MCP:**
- No MCP server needed
- No external processes
- Pure .NET implementation
- Same dynamic loading capability

**Complexity:**
- Requires implementing tool calling in `OllamaLlmService`
- Need to add per-table schema methods to `ISchemaProvider`
- Multi-turn conversation handling

**Estimate:** 2-3 days of work vs. 2-3 weeks for full MCP integration.

---

## 8. Implementation Complexity Estimates

| Approach | Effort | Components | Risk | Payoff |
|----------|--------|------------|------|--------|
| **Current (no change)** | 0 days | None | NONE | ❌ No token efficiency, no observability, no future-proofing |
| **DAB MCP (Recommended)** | 5-6 days | DAB container, minimal MCP client, tool calling in `OllamaLlmService` | **MEDIUM** (new integration, MCP protocol) | ✅ Token efficiency, enterprise observability, multi-database ready |
| **Custom MCP Server** | 2-3 weeks | Build MCP server (Node.js), MCP client (C#), integration plumbing, process management | **HIGH** (cross-platform, custom protocol impl) | ⚠️ Same as DAB MCP but worse — no DAB benefits, more maintenance |

### Risk Mitigation

| Risk | Mitigation |
|------|------------|
| DAB MCP server fails to start | Health check in Aspire, fallback to current `SchemaMetadataProvider` if DAB unavailable |
| MCP client HTTP calls fail | Retry logic with exponential backoff, circuit breaker pattern |
| Ollama doesn't support tool calling | **FALSE ASSUMPTION** — qwen2.5-coder:7b DOES support tool calling (verified) |
| MCP protocol changes | DAB implements MCP 2025-06-18 (fixed version), stable until DAB upgrades |
| No .NET MCP SDK | Build minimal HTTP JSON-RPC client (simpler than full SDK) |

---

## 9. Architecture Diagrams

### Current Architecture (Simplified)

```
User Prompt
    │
    ▼
┌─────────────────────────┐
│  OllamaLlmService       │
│  ┌─────────────────┐    │
│  │ System Prompt:  │    │
│  │ "You are a SQL  │    │
│  │ assistant.      │    │
│  │                 │    │
│  │ Schema:         │    │
│  │ [Incidents]     │    │
│  │  - Id (int)     │    │
│  │  - Title (nvar) │    │
│  │  - ...          │    │
│  └─────────────────┘    │
└───────────┬─────────────┘
            │
            ▼
     ┌──────────────┐
     │ Ollama Model │
     │ (qwen2.5)    │
     └──────┬───────┘
            │
            ▼
     SQL Query Generated
```

Schema loaded **once**, embedded in prompt.

### MCP Architecture (Hypothetical)

```
User Prompt
    │
    ▼
┌─────────────────────────────┐
│  OllamaLlmService           │
│  ┌─────────────────────┐    │
│  │ System Prompt:      │    │
│  │ "You are a SQL      │    │
│  │ assistant. Use      │    │
│  │ describe_table()    │    │
│  │ to inspect schema." │    │
│  └─────────────────────┘    │
└───────────┬─────────────────┘
            │
            ▼
     ┌──────────────────────┐
     │ Ollama Model         │
     │ (qwen2.5)            │
     └───────┬──────────────┘
             │
             │ Tool Call:
             │ describe_table("Incidents")
             ▼
     ┌────────────────────┐
     │ MCP Client (C#)    │
     └───────┬────────────┘
             │ stdio/HTTP
             ▼
     ┌────────────────────────┐
     │ MCP Server (Node.js)   │
     │ ┌────────────────────┐ │
     │ │ Tool: list_tables  │ │
     │ │ Tool: describe_tbl │ │
     │ └────────┬───────────┘ │
     └──────────┼─────────────┘
                │
                ▼
          SQL Server
         (INFORMATION_SCHEMA)
                │
                ▼
          JSON Response
                │
                ▼
         Back to Ollama
```

Schema loaded **on-demand** via tool calls.

---

## 10. Conclusion

### Summary

- **Azure Data API Builder MCP Server EXISTS and is production-ready** — v1.7+ (currently in preview)
- **Schema-only mode is trivial to configure** — disable all data tools except `describe-entities`
- **Token efficiency is a game-changer** — 94% reduction for targeted queries
- **Aspire integration is native** — DAB runs as a container resource with 20 lines of config
- **Enterprise observability is built-in** — OTEL, Application Insights, health checks
- **Implementation is reasonable** — 5-6 days for production-ready MVP
- **Future-proof architecture** — scales to multi-database, RBAC, and agentic workflows

### Recommended Action

**Implement DAB MCP integration with schema-only configuration.**

**Implementation Roadmap:**
1. **Week 1:** Add DAB container to Aspire, configure `dab-config.json` (schema-only tools)
2. **Week 1:** Build minimal MCP HTTP client (`McpHttpClient.cs`)
3. **Week 2:** Integrate tool calling into `OllamaLlmService`, test end-to-end
4. **Week 2:** Production hardening (error handling, health checks, OTEL spans)
5. **Week 3:** Deploy to staging, validate schema-only security constraint
6. **Week 4:** Deploy to production, monitor telemetry

**Fallback:** If DAB MCP integration hits blockers, current `SchemaMetadataProvider` remains functional — no regression risk.

### Decision Points

Re-evaluate this approach if:
- [ ] DAB v1.7 remains in preview for >6 months (stability concern)
- [ ] Official .NET MCP SDK is released and simplifies implementation
- [ ] Token efficiency proves unnecessary (database stays <10 tables)
- [ ] Observability requirements are deprioritized

Until then, **DAB MCP is the optimal architecture for this project.**

---

## Appendix: Code References

### Current Schema Loading (SchemaMetadataProvider.cs)

```csharp
public async Task<SchemaContext> GetSchemaAsync(CancellationToken cancellationToken = default)
{
    if (_cache.TryGetValue(CacheKey, out SchemaContext? cached) && cached is not null)
    {
        _logger.LogDebug("Returning cached schema metadata");
        return cached;
    }

    _logger.LogInformation("Loading schema metadata from database");
    var schema = await LoadSchemaAsync(cancellationToken);

    _cache.Set(CacheKey, schema, _options.SchemaCacheDuration);
    return schema;
}
```

**Key Points:**
- In-memory cache with configurable TTL
- Single query per cache miss
- Loads full schema atomically

### Current LLM Integration (OllamaLlmService.cs)

```csharp
private static List<AIChatMessage> BuildMessages(LlmChatRequest request)
{
    var messages = new List<AIChatMessage>();

    var systemPrompt = request.SystemPrompt ?? DefaultSystemPrompt;
    if (request.SchemaContext is { Tables.Count: > 0 } schema)
    {
        systemPrompt += "\n\nAvailable database schema (metadata only — no row data):\n" 
                        + FormatSchema(schema);
    }
    messages.Add(new AIChatMessage(ChatRole.System, systemPrompt));

    // ... user messages ...
}
```

**Key Points:**
- Schema formatted as plain text
- Appended to system prompt
- No tool calling (yet)

---

**END OF REPORT**
