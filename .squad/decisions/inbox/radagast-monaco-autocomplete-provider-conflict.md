# Decision: Disable Monaco Built-in SQL Autocomplete

**Date:** 2025-01-27  
**Status:** Implemented  
**Decision Maker:** Radagast (LLM Engineer)

## Context

User reported autocomplete suggestions appearing briefly then immediately disappearing in the SQL editor:
> "it feels like two embedding methods are fighting eachother, occaisionally I can see 'dbo.Accounts' in the list of suggestions but then it immeditely dissapears"

This was a classic race condition symptom.

## Problem

Monaco Editor has multiple built-in autocomplete sources:
1. **Language-specific providers** (SQL keywords, built-in functions)
2. **Word-based suggestions** (autocomplete from words found in the current document)
3. **Custom providers** (our embedding-based schema completion)

When multiple providers return results simultaneously, Monaco's suggestion widget shows inconsistent/flickering behavior — suggestions appear and vanish as different providers resolve at different times.

## Investigation

- Only ONE custom completion provider registered: `monaco.languages.registerCompletionItemProvider('sql', ...)`
- Monaco's built-in SQL language support was enabled by default
- Both the custom embedding provider and Monaco's built-in providers were firing on trigger characters (`.`, space)
- Backend API latency (~100-500ms for embeddings) vs instant word-based matching created a race

## Decision

**Disable Monaco's built-in suggestion providers and use ONLY the custom embedding-based provider.**

Changes to `TabbedSqlEditor.tsx` editor options:
```typescript
quickSuggestions: false,          // Disable automatic suggestions
wordBasedSuggestions: 'off',      // Disable word-based completions
```

Kept enabled:
```typescript
suggestOnTriggerCharacters: true  // Still trigger on '.' and space for custom provider
```

## Rationale

1. **Eliminates race condition** — Only one source of truth for suggestions
2. **Better UX** — Consistent, predictable autocomplete behavior
3. **Preserves embedding intelligence** — Backend embedding model determines what to show based on context
4. **No loss of functionality** — Custom provider already returns SQL keywords, tables, columns, etc.

## Trade-offs

- **Lost:** Monaco's instant word-based autocomplete (was competing/flickering anyway)
- **Gained:** Stable, context-aware autocomplete from embeddings
- **Risk:** If backend API is slow/down, no autocomplete at all (vs fallback to word matching)
  - Mitigated by graceful error handling in provider (returns empty on error)

## Alternative Considered

**Keep both providers but increase debouncing** — Rejected because:
- Still creates unpredictable UX (which provider wins?)
- Doesn't solve the fundamental race condition
- Embedding-based provider is more intelligent anyway

## Implementation

Single change to `TabbedSqlEditor.tsx`:
```diff
  options={{
    // ... existing options ...
    suggestOnTriggerCharacters: true,
    tabSize: 4,
+   quickSuggestions: false,
+   wordBasedSuggestions: 'off',
  }}
```

## Outcome

Users should now see stable autocomplete suggestions that appear and stay visible until dismissed or selected.
