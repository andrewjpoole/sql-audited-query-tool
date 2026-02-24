# Architecture Proposal: Ollama Embeddings for Monaco SQL Autocomplete

**By:** Gandalf (Lead)  
**Date:** 2026-02-23  
**Status:** PROPOSED  
**Requested by:** Andrew  

---

## Problem Statement

The Monaco SQL editor currently has **no custom autocomplete**. It relies on Monaco's built-in SQL language support, which knows SQL keywords but nothing about our database schema, query history, or user patterns. We want to use Ollama embeddings to provide intelligent, context-aware SQL assistance directly in the editor.

## Three Approaches Evaluated

| Approach | Description | Latency | Value | Complexity |
|----------|-------------|---------|-------|------------|
| **A. Schema-Aware Completions** | CompletionItemProvider powered by semantic search over schema | Low (~50ms) | High | Medium |
| **B. Inline Query Suggestions** | InlineCompletionsProvider suggesting full queries from history | Medium (~200ms) | High | High |
| **C. Semantic History Search** | Search panel finding past queries by natural language intent | Low (~100ms) | Medium | Low |

### Recommendation: **All three, phased delivery**

Phase 1 delivers the highest-value, lowest-risk feature (schema completions). Phase 2 adds the "wow factor" (inline suggestions). Phase 3 rounds it out with semantic search.

---

## Architecture Overview

### Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Frontend (React + Monaco)                                   │
│                                                              │
│  ┌──────────────────┐  ┌──────────────────┐                 │
│  │ CompletionProvider│  │ InlineCompletion  │                 │
│  │ (Phase 1)        │  │ Provider (Phase 2)│                 │
│  └────────┬─────────┘  └────────┬──────────┘                │
│           │                      │                            │
│           ▼                      ▼                            │
│  ┌─────────────────────────────────────────┐                 │
│  │  /api/completions/schema     (Phase 1)  │                 │
│  │  /api/completions/inline     (Phase 2)  │                 │
│  │  /api/completions/search     (Phase 3)  │                 │
│  └─────────────────────┬───────────────────┘                 │
└─────────────────────────┼───────────────────────────────────┘
                          │
┌─────────────────────────┼───────────────────────────────────┐
│  Backend (ASP.NET Core) │                                    │
│                         ▼                                    │
│  ┌──────────────────────────────────────┐                    │
│  │  EmbeddingCompletionService          │                    │
│  │  - Receives context (cursor, text)   │                    │
│  │  - Queries vector store              │                    │
│  │  - Ranks and returns completions     │                    │
│  └──────────────┬───────────────────────┘                    │
│                 │                                             │
│     ┌───────────┼───────────┐                                │
│     ▼           ▼           ▼                                │
│  ┌────────┐ ┌────────┐ ┌────────────┐                       │
│  │ Vector │ │Schema  │ │ Query      │                       │
│  │ Store  │ │Provider│ │ History    │                       │
│  │(in-mem)│ │(cached)│ │ Store      │                       │
│  └───┬────┘ └────────┘ └────────────┘                       │
│      │                                                       │
│      ▼                                                       │
│  ┌──────────────────────────────────────┐                    │
│  │  EmbeddingService                    │                    │
│  │  - Wraps Ollama embedding endpoint   │                    │
│  │  - Batch + cache embeddings          │                    │
│  └──────────────┬───────────────────────┘                    │
└─────────────────┼───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│  Ollama (Aspire-managed)                                     │
│  ┌────────────────────┐  ┌────────────────────┐             │
│  │ qwen2.5-coder:7b   │  │ nomic-embed-text   │             │
│  │ (chat model)       │  │ (embedding model)  │             │
│  └────────────────────┘  └────────────────────┘             │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

#### Phase 1: Schema Completions
```
1. App startup → SchemaMetadataProvider fetches schema
2. EmbeddingService embeds each table/column description
   e.g., "dbo.Users.email - varchar(255) NOT NULL - user email address"
3. Embeddings stored in InMemoryVectorStore
4. User types in Monaco → CompletionProvider fires
5. Frontend sends POST /api/completions/schema
   { "prefix": "SELECT u.em", "context": "FROM Users u", "cursorLine": 1 }
6. Backend embeds the prefix+context → cosine similarity search
7. Returns ranked CompletionItems: [email, emailVerified, ...]
8. Monaco displays completion dropdown
```

#### Phase 2: Inline Query Suggestions
```
1. Each executed query → embed and store in vector index
2. User types partial query → debounced (300ms) trigger
3. Frontend sends POST /api/completions/inline
   { "currentLine": "SELECT * FROM Orders WHERE", "fullText": "..." }
4. Backend finds similar past queries by embedding similarity
5. Returns inline ghost text suggestions
6. User accepts with Tab or dismisses
```

#### Phase 3: Semantic History Search
```
1. User opens search panel, types "find queries about user permissions"
2. Frontend sends POST /api/completions/search
   { "query": "user permissions", "limit": 10 }
3. Backend embeds query → searches query history embeddings
4. Returns ranked list of past queries with similarity scores
5. User clicks to insert into editor
```

---

## Key Technical Decisions

### 1. Embedding Model: `nomic-embed-text`
- **Why:** 768-dimension vectors, 8192 token context, fast inference, runs well in Ollama
- **Alternative:** `mxbai-embed-large` (1024-dim, higher quality, slower) — upgrade path if needed
- **Tradeoff:** `nomic-embed-text` is ~137M params, fast enough for real-time completions (~10-20ms per embedding on modern hardware)

### 2. Vector Store: In-Memory (Phase 1-2), Optional Qdrant (Phase 3+)
- **Why in-memory:** No new infrastructure. Schema metadata is small (~100-500 items). Query history is bounded. Aspire already manages enough services.
- **Implementation:** `InMemoryVectorStore` class with `ConcurrentDictionary<string, (float[] Embedding, T Metadata)>` and brute-force cosine similarity
- **Scale threshold:** If >10,000 embedded items, consider Qdrant via Aspire container
- **Tradeoff:** In-memory is lost on restart. Schema re-embeds on startup (fast). Query history embeddings should persist to file or DB eventually.

### 3. Where Similarity Search Happens: Backend Only
- **Why:** Embeddings require Ollama API call. Search logic stays server-side. Frontend never sees raw vectors.
- **Alternative considered:** Ship vectors to frontend, do similarity in JS — rejected (exposes embedding data, wasteful bandwidth, no security boundary)

### 4. Monaco Integration: Two Separate Providers
- **CompletionItemProvider** (Phase 1): Triggers on `.` (dot notation), space after keywords (SELECT, FROM, WHERE, JOIN, ON), and manual Ctrl+Space
- **InlineCompletionsProvider** (Phase 2): Triggers on typing pause (300ms debounce), shows ghost text like Copilot
- **Why separate:** Different trigger conditions, different UX patterns, different data sources (schema vs. history)

### 5. Embedding What Data

| Data Source | When Embedded | Dimensions | Refresh Strategy |
|-------------|---------------|------------|-----------------|
| Table names + descriptions | App startup + schema cache refresh | 768 | Re-embed on schema change (5-min cache) |
| Column names + types + constraints | App startup | 768 | Same as tables |
| Foreign key relationships | App startup | 768 | Same as tables |
| Query history (user + AI) | On execution | 768 | Append-only, embed once |
| Common SQL patterns/templates | App startup (static) | 768 | Hardcoded, rarely changes |

### 6. Performance Budget

| Operation | Target Latency | Strategy |
|-----------|---------------|----------|
| Embedding generation (single) | <20ms | Ollama local inference |
| Embedding generation (batch startup) | <2s for 500 items | Batch API calls, parallel |
| Similarity search (in-memory) | <5ms | Brute-force cosine, small dataset |
| Total completion round-trip | <100ms | Cached embeddings + fast search |
| Inline suggestion round-trip | <300ms | Debounced, async, cancellable |

### 7. Security Considerations
- **No data exposure:** Embeddings are generated from schema metadata and query text only. No row data is embedded.
- **Readonly enforcement:** Completion suggestions are text only — they don't execute anything. Execution still goes through the existing validated pipeline.
- **Audit trail:** Query history embeddings use the same audited queries. No new data paths.
- **Local only:** All embedding generation happens in Ollama (local). No data leaves infrastructure.

---

## Aspire Integration

### New Ollama Model Resource
```csharp
// In AppHost Program.cs
var ollama = builder.AddOllama("ollama")
    .WithDataVolume();

var chatModel = ollama.AddModel("ollamaModel", "qwen2.5-coder:7b");
var embedModel = ollama.AddModel("ollamaEmbed", "nomic-embed-text");  // NEW

builder.AddProject<Projects.SqlAuditedQueryTool_App>("app")
    .WithReference(chatModel)
    .WithReference(embedModel);  // NEW
```

### New Service Registration
```csharp
// In App Program.cs
builder.AddOllamaApiClient("ollamaEmbed");  // NEW - embedding client

// In Llm ServiceCollection extensions
services.AddSingleton<IEmbeddingService, OllamaEmbeddingService>();
services.AddSingleton<IVectorStore, InMemoryVectorStore>();
services.AddScoped<ICompletionService, EmbeddingCompletionService>();
```

---

## New Interfaces (Core Project)

```csharp
// Core/Interfaces/Llm/IEmbeddingService.cs
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IEnumerable<string> texts, CancellationToken ct = default);
}

// Core/Interfaces/Llm/IVectorStore.cs
public interface IVectorStore
{
    Task UpsertAsync(string key, float[] embedding, VectorMetadata metadata, CancellationToken ct = default);
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(float[] query, int topK = 10, string? category = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

// Core/Interfaces/Llm/ICompletionService.cs
public interface ICompletionService
{
    Task<IReadOnlyList<CompletionItem>> GetSchemaCompletionsAsync(CompletionContext context, CancellationToken ct = default);
    Task<IReadOnlyList<InlineSuggestion>> GetInlineSuggestionsAsync(InlineContext context, CancellationToken ct = default);
    Task<IReadOnlyList<QuerySearchResult>> SearchQueriesAsync(string query, int limit = 10, CancellationToken ct = default);
}
```

---

## Frontend Integration (Monaco)

### Phase 1: CompletionItemProvider
```typescript
// Register in SqlEditor.tsx on mount
monaco.languages.registerCompletionItemProvider('sql', {
  triggerCharacters: ['.', ' '],
  provideCompletionItems: async (model, position) => {
    const textUntilPosition = model.getValueInRange({
      startLineNumber: 1, startColumn: 1,
      endLineNumber: position.lineNumber, endColumn: position.column
    });
    const response = await fetch('/api/completions/schema', {
      method: 'POST',
      body: JSON.stringify({ prefix: textUntilPosition, cursorLine: position.lineNumber })
    });
    const items = await response.json();
    return { suggestions: items.map(toMonacoCompletionItem) };
  }
});
```

### Phase 2: InlineCompletionsProvider
```typescript
monaco.languages.registerInlineCompletionsProvider('sql', {
  provideInlineCompletions: async (model, position, context, token) => {
    const currentLine = model.getLineContent(position.lineNumber);
    const response = await fetch('/api/completions/inline', {
      method: 'POST',
      body: JSON.stringify({ currentLine, fullText: model.getValue() }),
      signal: token // cancellation
    });
    const suggestions = await response.json();
    return { items: suggestions.map(toInlineCompletion) };
  },
  freeInlineCompletions: () => {}
});
```

---

## Implementation Phases

### Phase 1: Schema-Aware Completions (2-3 sprints)
**Goal:** Type `SELECT u.` and get column completions from the Users table.

| Task | Owner | Effort |
|------|-------|--------|
| Add `nomic-embed-text` to Aspire | Samwise | Small |
| `IEmbeddingService` + `OllamaEmbeddingService` | Radagast | Medium |
| `IVectorStore` + `InMemoryVectorStore` | Radagast | Medium |
| Embed schema on startup (background) | Radagast | Medium |
| `POST /api/completions/schema` endpoint | Samwise | Medium |
| `CompletionItemProvider` in Monaco | Legolas | Medium |
| Security review of embedding pipeline | Faramir | Small |

### Phase 2: Inline Query Suggestions (2 sprints)
**Goal:** Start typing a query and see ghost-text suggestions from similar past queries.

| Task | Owner | Effort |
|------|-------|--------|
| Embed queries on execution (hook into pipeline) | Radagast | Medium |
| `POST /api/completions/inline` endpoint | Samwise | Medium |
| `InlineCompletionsProvider` in Monaco | Legolas | Medium |
| Debouncing + cancellation logic | Legolas | Small |
| Performance testing + tuning | Gandalf | Small |

### Phase 3: Semantic Query Search (1 sprint)
**Goal:** Search past queries by intent: "queries about order totals"

| Task | Owner | Effort |
|------|-------|--------|
| `POST /api/completions/search` endpoint | Samwise | Small |
| Search UI panel in frontend | Legolas | Medium |
| Integration with query history panel | Legolas | Small |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Ollama embedding latency too high for completions | UX feels sluggish | Pre-embed schema on startup; cache; use fast model |
| `nomic-embed-text` model not pulled on dev machines | Feature silently fails | Aspire model pull on startup; graceful degradation (fall back to substring matching) |
| In-memory vector store lost on restart | History suggestions reset | Phase 2+: persist embeddings to file/SQLite alongside query history |
| Embedding model changes dimensions | Store incompatible | Version embeddings; re-embed on model change |
| Too many completion requests | Ollama overloaded | Debounce (300ms), request cancellation, max concurrent requests |

---

## Decision Summary

1. **Use `nomic-embed-text`** via Ollama for embedding generation — fast, local, good quality
2. **In-memory vector store** for Phase 1-2 — no new infra, schema is small
3. **Backend-only similarity search** — security boundary, no raw vectors to frontend
4. **Two Monaco providers** — CompletionItemProvider (schema) + InlineCompletionsProvider (history)
5. **Phased delivery** — schema completions first (highest value), then inline, then search
6. **Graceful degradation** — if embedding model unavailable, fall back to existing behavior (no completions)
7. **All three approaches are complementary** — they serve different use cases and share the same embedding infrastructure
