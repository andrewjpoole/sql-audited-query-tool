### 2026-02-22: LLM Integration Architecture — Radagast

**By:** Radagast (LLM Engineer)

**What:** Established LLM integration patterns for the project.

**Key Decisions:**
1. **LLM interfaces in `Core/Interfaces/Llm/`** — separated from Samwise's DB interfaces to avoid conflicts. ILlmService, IQueryAssistant, ISchemaProvider live here.
2. **IConnectionFactory in `Core/Interfaces/`** — shared interface for DB connections. Samwise's Database project should implement this; SchemaMetadataProvider consumes it.
3. **Ollama as local LLM backend** — HTTP client talks to `localhost:11434/api/chat`. Model configurable via `Llm` config section (default: `llama3.2`).
4. **Streaming via SSE** — `POST /api/chat` supports `stream: true` for real-time token delivery to the frontend using Server-Sent Events.
5. **Query classification** — SQL responses are parsed from markdown code blocks and classified as read-only (SELECT) or fix query (INSERT/UPDATE/DELETE/etc). Fix queries are clearly labeled and never auto-executed.
6. **Schema safety boundary** — SchemaMetadataProvider queries ONLY `INFORMATION_SCHEMA.TABLES` and `INFORMATION_SCHEMA.COLUMNS`. Returns table names, column names, data types, nullability, max length. NEVER row data.
7. **DI lifetimes** — OllamaLlmService registered as typed HttpClient (scoped), QueryAssistant and SchemaProvider are scoped, MemoryCache is singleton.
8. **Schema caching** — 5-minute default via `IMemoryCache`, configurable in appsettings.

**Impact on team:**
- **Samwise:** Please implement `IConnectionFactory` in SqlAuditedQueryTool.Database (interface is in `Core/Interfaces/IConnectionFactory.cs`). It should return a readonly DbConnection.
- **Legolas:** Frontend can call `POST /api/chat` (with `stream: true` for SSE), `POST /api/query/suggest`, and `GET /api/schema`.
- **Faramir:** Review the safety boundary — LLM prompts, SchemaContext model, and SchemaMetadataProvider query are the critical points.
