# Decision: Security Contracts for Query Pipeline

**Date:** 2026-02-22T14:20:00Z
**By:** Faramir (Security)
**Status:** Active

## What

Established three security enforcement contracts in `SqlAuditedQueryTool.Core/Security/`:

1. **SqlValidator** — All SQL entering the pipeline MUST pass `ValidateReadOnly()` before execution. Any `RiskLevel.Blocked` result means the query is rejected. `Suspicious` results may proceed with logging.
2. **DataLeakPrevention** — All payloads sent to the LLM MUST pass `ValidateLlmPayload()`. Schema metadata only — no row data, no PII.
3. **AuditIntegrity** — Every audit entry MUST include an integrity hash from `GenerateAuditHash()`. Verification via `VerifyAuditHash()` confirms no tampering.

## Contracts for Other Team Members

- **Samwise (Backend):** Call `SqlValidator.ValidateReadOnly()` before executing any query. Call `SqlValidator.SanitizeForAudit()` before writing SQL to audit logs. Call `AuditIntegrity.GenerateAuditHash()` when creating audit entries.
- **Radagast (LLM):** Call `DataLeakPrevention.ValidateLlmPayload()` on every payload before sending to the LLM. Only schema metadata (table names, column names, types) should be in payloads — never row data.
- **Legolas (Frontend):** Display `ValidationResult.Violations` to users when queries are rejected. Show `RiskLevel` indicator on query results.

## Why

- Readonly enforcement prevents accidental or malicious data mutation
- Data leak prevention ensures no PII or row data reaches the LLM
- Audit integrity hashing provides tamper detection for compliance

## Blocked Keywords

INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE, GRANT, REVOKE, DENY, sp_*, xp_*
