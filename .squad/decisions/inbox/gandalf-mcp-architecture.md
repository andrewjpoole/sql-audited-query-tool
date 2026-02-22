# Architectural Decision: SQL Server MCP Integration

**Date:** 2026-02-22 (Revised: 2026-02-22)  
**Author:** Gandalf (Lead/Architect)  
**Status:** REVISED — RECOMMEND MCP INTEGRATION  
**Topic:** Should we use SQL Server MCP to give Ollama direct database access?

---

## ⚠️ REVISION NOTICE

This decision has been **REVISED** based on Andrew's clarification:

> *"the 'strictly without exposing any data' requirement doesn't apply to locally running ollama models"*

**Key Insight:** The original security constraint was about preventing data leakage to **external services**. Since Ollama runs locally on Andrew's infrastructure, data never leaves the local environment. Local LLMs CAN access database data.

---

## Context

Andrew asked: *"we need to consider the sql server MCP server and whether we can use that to give the ollama model access to the database"*

**Original Requirement (now clarified):**
> "the app should also run a local LLM model with SQL Server MCP to aid with queries, but **strictly without exposing any data from the database**"

**Clarified Requirement:**
> Local Ollama models MAY access database data because they run on local infrastructure and data never leaves the environment.

**Current Architecture:**
- `SchemaMetadataProvider` queries INFORMATION_SCHEMA and sys.* views
- LLM receives schema context (tables, columns, PKs, FKs, indexes) — NO row data
- Users execute queries through our controlled readonly pipeline
- All queries are audited to GitHub issues

---

## Research Findings

### SQL Server MCP Options Evaluated

1. **mssqlclient-mcp-server** (C#/.NET) — `aadversteeg/mssqlclient-mcp-server`
   - Query execution is **configurable** via flags
   - Configuration flags:
     - `EnableExecuteQuery` (default: false) — **ENABLE FOR OUR USE CASE**
     - `EnableExecuteStoredProcedure` (default: false)
   - Built with C# MCP SDK — native integration with our .NET stack

2. **microsoft_sql_server_mcp** (Python) — `RichardHan/mssql_mcp_server`
   - Exposes: list tables, execute SQL (SELECT, INSERT, UPDATE, DELETE)
   - Python-based — requires process bridge from .NET

### Ollama MCP Connectivity (Radagast Research)

**Key Finding:** Ollama does NOT natively support MCP protocol, but HAS native tool calling since v0.6+ (July 2024).

**Bridge Options:**
1. **ollama-mcp-bridge** (Node.js) — `patruff/ollama-mcp-bridge` (962 stars)
   - Acts as transparent proxy between Ollama and MCP servers
   - Translates Ollama tool calls → MCP JSON-RPC
   - Handles multi-round tool orchestration
   - Supports streaming responses
   
2. **.NET App Orchestration** — Custom solution
   - Our app intercepts Ollama structured output
   - App makes MCP calls on Ollama's behalf
   - Results fed back to Ollama for reasoning
   - Full control over audit pipeline

---

## Security Analysis (REVISED)

### New Security Context: Local-Only Processing

| Factor | Assessment |
|--------|------------|
| **Data Residency** | ✅ All data stays on local infrastructure |
| **External Exposure** | ✅ NONE — Ollama runs locally, no cloud API calls |
| **Network Boundary** | ✅ localhost only — no data leaves the machine |
| **Audit Trail** | ✅ All queries still logged to GitHub issues |
| **Read-Only Enforcement** | ✅ Database connection uses readonly flag |

### Remaining Risks (Low Severity)

| Risk | Severity | Mitigation |
|------|----------|------------|
| **Prompt Injection** | LOW | LLM only has read access; worst case = SELECT queries |
| **Excessive Querying** | LOW | Rate limiting at MCP/app layer; audit provides visibility |
| **Schema Discovery** | NONE | Already intentional — schema access aids query suggestions |

**Security Verdict:** With local-only Ollama, data exposure via MCP is **architecturally acceptable**.

---

## Architectural Options (REVISED)

### Option A: MCP Bridge Architecture ✅ (NOW RECOMMENDED)
**Ollama + MCP Bridge + SQL Server MCP with Query Execution ENABLED**

```
User ↔ Chat UI ↔ App
                  ↓
    MCP Bridge (Node.js/Python)
         ↙            ↘
   Ollama        SQL Server MCP (C#)
                      ↓
                 SQL Server
                      ↓
            Query Results + Schema
```

**Pros:**
- ✅ **Dramatically improved assistance** — LLM can see actual data, reason about results
- ✅ **Iterative investigation** — LLM can query, analyze, refine, suggest follow-ups
- ✅ **Standard MCP pattern** — extensible to other tools later
- ✅ **Schema + Data context** — better query suggestions based on actual table contents

**Cons:**
- ⚠️ Additional process (MCP bridge) to manage
- ⚠️ Ollama doesn't natively support MCP — requires adapter
- ⚠️ MCP queries need to flow through our audit pipeline

### Option B: App-Orchestrated MCP ✅ (ALTERNATIVE)
**Our .NET app handles MCP calls on behalf of Ollama**

```
User ↔ Chat UI ↔ App
                  ↓
            OllamaLlmService
                  ↓ (function calling)
       App detects tool requests
                  ↓
       App calls MCP or QueryExecutor
                  ↓
       Results fed back to Ollama
                  ↓
            AuditService (GitHub)
```

**Pros:**
- ✅ **Full audit control** — every query flows through our AuditService
- ✅ **No external processes** — .NET app handles everything
- ✅ **Leverages existing QueryExecutor** — readonly enforcement
- ✅ **Simpler deployment** — no Node.js dependency

**Cons:**
- ⚠️ More implementation work in C#
- ⚠️ Less flexible than standard MCP

### Option C: Keep Current Architecture (Previously Recommended)
**SchemaMetadataProvider + Human-Executed Queries**

**Assessment:** Still viable but now **suboptimal**. We lose significant value:
- LLM suggests queries "blindly" without seeing actual data
- No iterative refinement based on results
- Incident investigation requires more back-and-forth

---

## Decision: OPTION B — App-Orchestrated MCP (RECOMMENDED)

### Rationale

Given the clarified security context, I recommend **Option B** because:

1. **Best of Both Worlds:**
   - Ollama gets data access (improved assistance quality)
   - All queries flow through our app (complete audit trail)
   - We control the execution path (readonly enforcement)

2. **Audit Trail Preservation:**
   - The external MCP bridge (Option A) might bypass our GitHub audit
   - App-orchestrated approach ensures EVERY query is logged
   - Andrew specifically requested audit trail for incident investigation

3. **Architecture Alignment:**
   - Our existing `QueryExecutor` already handles readonly SQL execution
   - We add "tool calling" mode to `OllamaLlmService`
   - Results are fed back to Ollama for analysis
   - Natural extension of current architecture

4. **Deployment Simplicity:**
   - No Node.js/Python bridge process to manage
   - Single .NET application hosts everything
   - Aspire orchestration handles Ollama

### Implementation Approach

1. **Enable Ollama Tool Calling:**
   - Add tool definitions to `OllamaLlmService` (execute_query, get_schema)
   - Handle tool call responses in chat loop
   
2. **Orchestrate Queries:**
   - When Ollama requests `execute_query`, our app:
     - Validates query is readonly (SELECT only)
     - Executes via `QueryExecutor`
     - Logs to GitHub via `AuditService`
     - Returns results to Ollama
     
3. **Keep SchemaMetadataProvider:**
   - Still useful for schema context in system prompt
   - MCP `get_schema` tool can delegate to it

### What Changes From Current Architecture

| Component | Before | After |
|-----------|--------|-------|
| Schema Context | Embedded in system prompt | Tool-based discovery + prompt |
| Query Execution | Human via UI only | LLM can request (app executes) |
| Data Exposure | Never to LLM | LLM can see query results |
| Audit Trail | All queries logged | All queries still logged ✅ |
| Readonly Enforcement | App-level | App-level (unchanged) ✅ |

---

## Implementation Notes

### Phase 1: Enable Tool Calling in OllamaLlmService (Samwise)

1. Define tools:
   ```csharp
   public record ToolDefinition(string Name, string Description, JsonSchema Parameters);
   
   var tools = new[] {
       new ToolDefinition("execute_query", "Execute a readonly SQL query", querySchema),
       new ToolDefinition("get_schema", "Get table/column schema information", schemaSchema)
   };
   ```

2. Add tool handling loop:
   ```csharp
   while (response.HasToolCalls)
   {
       foreach (var toolCall in response.ToolCalls)
       {
           var result = await ExecuteToolAsync(toolCall);
           messages.Add(new ToolResultMessage(toolCall.Id, result));
       }
       response = await _chatClient.CompleteAsync(messages, tools);
   }
   ```

### Phase 2: Query Execution Tool (Samwise)

```csharp
private async Task<string> ExecuteToolAsync(ToolCall toolCall)
{
    switch (toolCall.Name)
    {
        case "execute_query":
            var sql = toolCall.Arguments.GetProperty("sql").GetString();
            
            // Validate readonly
            if (!QueryValidator.IsReadOnly(sql))
                return "Error: Only SELECT queries are allowed.";
            
            // Execute through existing pipeline (with audit)
            var result = await _queryExecutor.ExecuteAsync(sql, auditContext);
            return FormatResultsForLlm(result);
            
        case "get_schema":
            var tableName = toolCall.Arguments.GetProperty("table").GetString();
            var schema = await _schemaProvider.GetTableSchemaAsync(tableName);
            return FormatSchemaForLlm(schema);
            
        default:
            return $"Unknown tool: {toolCall.Name}";
    }
}
```

### Phase 3: Audit Integration

- All `execute_query` calls MUST flow through `AuditService`
- Include in audit log:
  - Source: "LLM (Ollama)" vs "User"
  - Query text
  - Row count returned
  - Timestamp
  - GitHub issue reference

---

## Tradeoffs Summary

| Factor | With MCP/Tool Calling | Without (Current) |
|--------|----------------------|-------------------|
| **Query Quality** | ⬆️ Much better — sees actual data | Lower — blind suggestions |
| **Investigation Flow** | ⬆️ Iterative, contextual | Manual iteration |
| **Implementation Effort** | ~2 weeks | Already done |
| **Architecture Complexity** | Moderate | Simple |
| **Audit Trail** | ✅ Preserved | ✅ Preserved |
| **Security** | ✅ Local only | ✅ Air gap |

---

## Final Recommendation

**IMPLEMENT OPTION B: App-Orchestrated Tool Calling**

This unlocks significant value for incident investigation:
- Ollama can query, analyze results, and suggest follow-up queries
- All queries remain audited to GitHub issues
- Readonly enforcement stays in place
- No external MCP bridge process needed

### Task Breakdown

1. **Samwise:** Add tool calling support to `OllamaLlmService`
2. **Samwise:** Implement `execute_query` tool with `QueryExecutor` integration
3. **Radagast:** Design prompt engineering for effective tool use
4. **Faramir:** Security review of tool calling implementation
5. **Legolas:** Update chat UI to show query results inline

---

## Signatures

**Decision Made By:** Gandalf (Lead/Architect)  
**Revision Reason:** Andrew clarified local Ollama can access data (security context changed)  
**Security Review:** Updated assessment above  
**LLM Integration Review:** Radagast confirmed feasibility via tool calling  

---

*This decision supersedes the original 2026-02-22 decision to reject MCP. The security context has fundamentally changed.*
