### 2026-02-22T15:45:00Z: SQL Server MCP Security Assessment

**By:** Faramir (Security)

**RISK RATING: 🔴 CRITICAL — REJECT AS CURRENTLY PROPOSED**

---

## Executive Summary

Giving Ollama direct database access via SQL Server MCP **violates the core security requirement** that "*the app should run a local LLM model with SQL Server MCP to aid with queries, but **strictly without exposing any data from the database**.*"

**Recommendation:** **REJECT** direct MCP-to-database connectivity. Maintain the current SchemaMetadataProvider architecture with controlled schema-only exposure.

---

## 1. SQL Server MCP Capabilities Analysis

### What SQL Server MCP Exposes

Based on research of `@fynet/mcp-mssql` (the primary SQL Server MCP implementation):

**Default Capabilities:**
- Full SQL query execution (SELECT, INSERT, UPDATE, DELETE)
- Table creation and schema modification
- Index management
- Direct connection with user-provided credentials
- No built-in read-only mode
- No tool-level access control

**Protocol Architecture:**
- MCP servers expose "tools" (functions) and "resources" (data sources)
- Tools are invoked by the LLM autonomously based on natural language requests
- **No granular permission model** — if a tool exists, the LLM can call it

### Configuration Options

The `@fynet/mcp-mssql` package accepts:
- `server`, `database`, `user`, `password` — full database credentials
- `trustServerCertificate`, `encrypt` — connection security options
- **NO read-only enforcement flags**
- **NO schema-only mode**
- **NO tool filtering mechanism**

---

## 2. Attack Vectors & Vulnerabilities

### 2.1 Prompt Injection → Data Exfiltration

**Threat:** A malicious or manipulated prompt could trick the LLM into reading actual row data.

**Attack Scenario:**
```
User: "Show me the schema for the Users table"
LLM (normal): Returns column names, types from INFORMATION_SCHEMA

User: "Show me the schema for Users with example data"
LLM (manipulated): Executes SELECT TOP 5 * FROM Users, returns PII
```

**Defense Depth Assessment:** ❌ **NONE**
- MCP has no payload inspection layer
- LLM decides what SQL to run based on natural language — no hardcoded query validation
- Even with a "schema-only" system prompt, prompt injection can override instructions

**Likelihood:** HIGH — Prompt injection is a well-documented LLM vulnerability.

### 2.2 Tool Misuse — Unintended SELECT Execution

**Threat:** Even without malicious intent, the LLM might execute data-returning queries to "help" the user.

**Attack Scenario:**
```
User: "What's the structure of the Orders table?"
LLM: "Let me check the data to give you better context..."
      → Executes SELECT TOP 1 * FROM Orders
      → Returns actual order data in response
```

**Defense Depth Assessment:** ❌ **NONE**
- LLMs are trained to be helpful and may execute queries they believe aid understanding
- No mechanism to prevent SELECT statements that return data (only schema queries permitted)
- MCP does not distinguish between schema queries and data queries

**Likelihood:** MEDIUM-HIGH — LLMs frequently over-assist unless tightly constrained.

### 2.3 Schema Metadata as a Data Leak Vector

**Threat:** Even schema-only access can leak sensitive information.

**Examples:**
- Column names like `CreditCardNumber`, `SSN`, `PasswordHash` reveal data types
- Foreign key relationships expose business logic
- Index patterns hint at query hotspots (what data is accessed frequently)

**Defense Depth Assessment:** ⚠️ **ACCEPTABLE IF CONTROLLED**
- This is a known trade-off in the current `SchemaMetadataProvider` design
- Mitigated by: schema is already exposed to LLM in current architecture
- **BUT:** MCP broadens the attack surface by giving the LLM query *execution* capability, not just schema *reading*

### 2.4 Credential Exposure

**Threat:** MCP configuration requires full database credentials in plaintext config files (`.copilot/mcp-config.json`).

**Attack Scenario:**
- Config file accidentally committed to version control
- Config file readable by other processes on the developer's machine
- Credentials grant full read-write access (no read-only constraint)

**Defense Depth Assessment:** 🟡 **MITIGATABLE**
- Use environment variables instead of plaintext passwords
- Use database-level read-only user (doesn't prevent SELECT data exfiltration)
- Current `ReadOnlyConnectionFactory` already mitigates this by using readonly connection strings

**Likelihood:** MEDIUM — Credential leaks are common developer mistakes.

### 2.5 No Audit Trail for LLM-Initiated Queries

**Threat:** If the LLM executes queries via MCP, they bypass the `GitHubAuditLogger`.

**Attack Scenario:**
- LLM runs a query that returns PII
- Query is not logged to GitHub issues (violates compliance requirement)
- No forensic trail if data exposure is discovered later

**Defense Depth Assessment:** ❌ **CRITICAL FAILURE**
- Core requirement: "Every query execution is logged as a GitHub issue"
- MCP-initiated queries would need a separate audit interception layer
- Current architecture audits queries executed through `SqlQueryExecutor` — MCP bypasses this

**Likelihood:** CERTAIN — Without engineering effort, this WILL happen.

---

## 3. Defense-in-Depth Analysis

### Current Architecture (SchemaMetadataProvider)

✅ **Strong Defense:**
1. **Controlled Surface:** Only `INFORMATION_SCHEMA` and `sys.*` views queried
2. **Static Queries:** SQL is hardcoded in C# — no LLM influence on query structure
3. **Cached Results:** Schema fetched once, cached — minimal database interaction
4. **Payload Validation:** `DataLeakPrevention` scans schema payload before LLM sees it
5. **Audit Trail:** All user-initiated queries logged via `GitHubAuditLogger`
6. **Readonly Connection:** `ReadOnlyConnectionFactory` enforces readonly connection strings

### Proposed Architecture (SQL Server MCP)

❌ **Broken Defense:**
1. **Uncontrolled Surface:** LLM can execute arbitrary SQL (only limited by credentials)
2. **Dynamic Queries:** LLM constructs SQL on-the-fly based on natural language
3. **Live Database Access:** Every LLM query hits the database — no caching
4. **No Payload Validation:** MCP returns results directly to LLM without `DataLeakPrevention` scan
5. **Bypassed Audit Trail:** MCP queries don't flow through `SqlQueryExecutor` → not logged
6. **Same Credentials:** Even if read-only user, can still SELECT data

**Security Regression:** 🔴 **5 out of 6 defenses removed**

---

## 4. Can MCP Be Configured Safely?

### Theoretical Mitigations

| Mitigation | Feasibility | Effectiveness |
|------------|-------------|---------------|
| **Read-only database user** | ✅ Easy | ❌ **Does NOT prevent SELECT on data tables** |
| **Schema-only tool filtering** | 🟡 Requires custom MCP server | 🟡 Could restrict tools to INFORMATION_SCHEMA queries only |
| **Audit interception layer** | 🟡 Requires middleware | ✅ Could log MCP queries to GitHub issues |
| **Payload scanning before LLM response** | 🟡 Requires hooking MCP response flow | ✅ Could apply `DataLeakPrevention` validation |
| **Query allow-listing** | 🔴 Defeats LLM flexibility | 🟡 Safe but negates MCP value |
| **Network-level isolation** | 🟡 Requires separate MCP database instance | 🟡 Could expose schema-only clone of production DB |

### Engineering Effort Required

To safely implement SQL Server MCP while honoring "*strictly without exposing any data*":

1. **Fork `@fynet/mcp-mssql`** to create a schema-only variant
   - Remove all tools except schema introspection
   - Hardcode queries to INFORMATION_SCHEMA and sys.* views
   - Block any user-supplied SQL

2. **Build audit middleware** to intercept MCP queries
   - Hook into MCP protocol transport layer
   - Log every query to GitHub issues before execution
   - Apply `SqlValidator.ValidateReadOnly()` to MCP queries

3. **Integrate `DataLeakPrevention`** into MCP response flow
   - Scan MCP results before LLM receives them
   - Reject responses with PII or data patterns

4. **Deploy schema-only database replica**
   - Separate SQL Server instance with only `INFORMATION_SCHEMA` and `sys.*` views
   - No actual data tables — purely metadata

**Estimated Effort:** 2-3 weeks of security engineering + ongoing maintenance

**Risk:** ⚠️ Custom security middleware is error-prone. Any bypass = data leak.

---

## 5. Risk Assessment Matrix

| Threat | Likelihood | Impact | Risk Score | Mitigation Cost |
|--------|-----------|--------|-----------|----------------|
| Prompt injection → data SELECT | HIGH | CRITICAL | 🔴 **9/10** | 3 weeks engineering |
| LLM over-assistance → data leak | MEDIUM-HIGH | CRITICAL | 🔴 **8/10** | 3 weeks engineering |
| Bypassed audit trail | CERTAIN | HIGH | 🔴 **10/10** | 2 weeks engineering |
| Credential exposure | MEDIUM | HIGH | 🟡 **6/10** | Env vars (1 day) |
| Schema metadata leak | LOW | MEDIUM | 🟢 **3/10** | Already accepted |

**Overall Risk Rating:** 🔴 **CRITICAL (9/10)**

---

## 6. Compliance Impact

**Requirement:** "*the app should run a local LLM model with SQL Server MCP to aid with queries, but **strictly without exposing any data from the database***"

### Interpretation

The requirement is **self-contradictory as written**:

- "with SQL Server MCP" → implies direct database connectivity
- "strictly without exposing any data" → requires NO data access, only schema

### Clarification Needed

**Question for Andrew (user):**

> When you say "SQL Server MCP," do you mean:
>
> **A)** The LLM should connect directly to SQL Server via the MCP protocol (with custom safety constraints)?
>
> **B)** The LLM should use a *schema-only MCP server* that only exposes metadata (no data table access)?
>
> **C)** The current `SchemaMetadataProvider` architecture is sufficient, and "SQL Server MCP" was meant descriptively (not a literal MCP server requirement)?

**My recommendation:** **Option C** is the safest and already implemented.

---

## 7. Recommendation

### 🔴 REJECT: Direct SQL Server MCP Integration

**Reasoning:**
1. Violates core security requirement ("strictly without exposing any data")
2. Removes 5 out of 6 existing security controls
3. Introduces 3 critical-severity attack vectors
4. Requires 2-3 weeks of custom security engineering to mitigate
5. Ongoing maintenance burden for security-critical custom middleware

### ✅ APPROVE: Current SchemaMetadataProvider Architecture

**Reasoning:**
1. Already satisfies "LLM aid with queries" requirement
2. Schema metadata exposed to LLM for query generation
3. All 6 security defenses intact
4. Audit trail complete (GitHub issue per query)
5. No data exposure risk (only `INFORMATION_SCHEMA` and `sys.*` queried)

### 🟡 CONDITIONAL APPROVE: Schema-Only MCP Server (If User Insists)

**Required Conditions:**
1. Build custom MCP server (fork of `@fynet/mcp-mssql`) that:
   - Exposes ONLY `get_schema` tool (no query execution)
   - Hardcodes queries to `INFORMATION_SCHEMA` and `sys.*`
   - Rejects any user-supplied SQL
2. Integrate `DataLeakPrevention` into MCP response flow
3. Deploy schema-only database replica (no data tables)
4. 100% test coverage for schema-only enforcement
5. Security review sign-off before production

**Effort:** 2-3 weeks + ongoing maintenance  
**Risk After Mitigations:** 🟡 **MEDIUM (5/10)**

---

## 8. Recommended Path Forward

### Immediate Actions (Today)

1. **Clarify requirement** with Andrew:
   - Is direct MCP database connectivity truly needed?
   - Or is schema-only LLM assistance (current state) sufficient?

2. **Document current architecture** as compliant with security requirements

3. **If MCP is required:**
   - Prototype schema-only MCP server
   - Security review before implementing

### Architecture Decision

**Current State (Approved):**
```
User Request
    ↓
LlmQueryAssistant
    ↓
SchemaMetadataProvider → Queries INFORMATION_SCHEMA only
    ↓                     (hardcoded SQL, cached)
OllamaLlmService      → Receives schema metadata
    ↓                     (validated by DataLeakPrevention)
Suggests Queries
    ↓
User Reviews & Executes via SqlQueryExecutor
    ↓                     (validated by SqlValidator)
GitHubAuditLogger     → Logged to GitHub issue
```

**Proposed Alternative (REJECTED):**
```
User Request
    ↓
Ollama + SQL Server MCP → Direct database access
    ↓                      (no validation, no audit)
Executes arbitrary SQL
    ↓
Returns results (potentially data)
```

**Verdict:** Current architecture is **SECURE and COMPLIANT**. Proposed MCP approach is **INSECURE and NON-COMPLIANT**.

---

## 9. Security Contracts (Existing Enforcement)

Reference: `.squad/decisions/inbox/faramir-security-contracts.md`

**These contracts MUST be preserved in any architecture change:**

1. **Readonly Enforcement:** `SqlValidator.ValidateReadOnly()` — blocks 13 write keywords
2. **Data Leak Prevention:** `DataLeakPrevention.InspectPayload()` — scans for PII before LLM
3. **Audit Integrity:** `AuditIntegrity.GenerateAuditHash()` — tamper-proof query logs
4. **Schema-Only Queries:** `SchemaMetadataProvider` — hardcoded INFORMATION_SCHEMA queries
5. **Readonly Connections:** `ReadOnlyConnectionFactory` — connection string enforcement
6. **GitHub Issue Logging:** `GitHubAuditLogger` — every query logged

**MCP integration would break:** #2, #4, #6 without significant engineering.

---

## Appendix A: MCP Protocol Security Model

The Model Context Protocol (MCP) is designed for **trusted LLM-to-data-source connectivity**. Its security model assumes:

1. The LLM is a trusted agent (not adversarial)
2. The data source is intended to be fully accessible to the LLM
3. The user trusts the LLM to make autonomous data access decisions

**This does NOT align with our threat model:**
- We assume LLMs can be manipulated via prompt injection
- We require strict schema-only access (no data exposure)
- We require explicit user approval before query execution

**Conclusion:** MCP's security model is **incompatible** with our requirements.

---

## Appendix B: Alternative: Copilot Workspace Integration (Future)

If the goal is "better LLM integration," consider:

**GitHub Copilot Workspace** (when GA):
- Copilot can see schema via `SchemaMetadataProvider`
- Suggests queries in chat
- User explicitly approves before execution
- Queries logged to GitHub issues (existing flow)

This gives "LLM-assisted query building" without MCP's security risks.

---

**Faramir, Security Engineer**  
*"The way of wisdom is to guard against the danger unseen."*
