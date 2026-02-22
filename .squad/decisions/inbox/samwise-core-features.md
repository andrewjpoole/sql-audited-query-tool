### 2026-02-22T13:00:00Z: Core Features & Aspire — Samwise
**By:** Samwise (Backend)
**What:** Implemented core features across Database, Audit, and App layers. Added .NET Aspire for local orchestration.

**Key decisions:**
1. **QueryResult carries row data** — Extended `QueryResult` with `Rows` (`IReadOnlyList<IReadOnlyDictionary<string, object?>>`) so the API can return actual query results. Previous model was metadata-only.
2. **Write-operation blocklist uses compiled regex** — 9 keywords blocked: INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE, EXEC, EXECUTE. Word-boundary matching prevents false positives on column names.
3. **Audit failure is non-blocking** — If GitHub API call fails, the query result still returns. Audit error is logged but doesn't break the user flow.
4. **Connection isolation** — Every connection gets `ApplicationIntent=ReadOnly` (SQL Server routing hint) AND `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` (no locks on production).
5. **Aspire SDK 9.2.1** — Pinned to 9.x to match net9.0 target framework. Template defaulted to 13.0.0/net10.0.
6. **EF Core 9.x** — Wildcard version resolution picked 10.0.3 (net10.0-only). Pinned to `9.*`.
7. **CORS** — Default policy allows `http://localhost:5173` (React dev server) with any header/method.

**Why:** These are the foundational backend capabilities required before the frontend can query databases with audit trails. Aspire enables local dev with a containerized SQL Server.

**Impact:** All team members — App's Program.cs now wires up all three service layers (Database, Audit, Llm).
