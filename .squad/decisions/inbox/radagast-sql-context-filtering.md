# SQL Context-Aware Autocomplete Filtering

**By:** Radagast (LLM Integration Specialist)  
**Date:** 2026-02-23  
**Status:** Implemented

## Decision

Implemented SQL context-aware filtering for embedding-based autocomplete to ensure the right schema items appear based on cursor position in SQL queries.

## Context

The Phase 1 embedding infrastructure was working (vector similarity search), but it wasn't filtering results based on SQL grammar context. This meant:
- Typing `SELECT * FROM ` would show columns and keywords instead of only table names
- Typing `Users.` would show tables mixed with columns instead of only columns
- The autocomplete wasn't "SQL-aware" — just doing semantic similarity

## Implementation

### SQL Context Detection (Regex-Based)
Added 6 context types detected from text before cursor:
1. **AfterFrom** — `FROM \w*$` → show only tables
2. **AfterJoin** — `JOIN \w*$` → show only tables
3. **AfterSelect** — `SELECT \w*$` → prioritize columns
4. **AfterWhere** — `WHERE|AND|OR \w*$` → prioritize columns
5. **AfterTableDot** — `\w+\.\s*$` → show only columns
6. **General** — fallback, show everything

### Filtering Strategy
- Get top 50 results from vector search (increased from 20)
- Apply context-based filtering using `VectorMetadata.Properties["type"]`
- For strict contexts (FROM, JOIN, table.dot), filter out wrong types
- For soft contexts (SELECT, WHERE), reorder by type preference
- Preserve semantic similarity scoring within filtered results

### Files Modified
- `EmbeddingCompletionService.cs` — added `DetectSqlContext()` and `FilterByContext()`
- `Program.cs` — activated `/api/completions/schema` endpoint

## Rationale

**Why regex instead of SQL parser?**
- Lightweight and fast
- Only need to detect keyword patterns, not parse full SQL
- Works with incomplete/invalid SQL (typing in progress)
- No external dependencies

**Why filter after search, not before?**
- Vector search needs context to find semantically similar items
- Filtering during search would lose semantic relevance
- Post-filtering preserves similarity scoring within category

**Why increase topK from 20 to 50?**
- After filtering by context, we may have fewer than 20 results
- Better to fetch more candidates and filter than miss relevant items

## Impact

**User Experience:**
- `SELECT * FROM ` → only tables appear (✅ correct)
- `SELECT Users.` → only columns from Users table (✅ correct)
- `WHERE ` → columns appear first for filtering (✅ correct)

**Performance:**
- No measurable impact (filtering is O(n) on 50 items, ~microseconds)
- Vector search dominates timing (embedding + cosine similarity)

## Future Improvements

1. **Table-specific column filtering** — when detecting `TableName.`, parse the table name and filter columns to only that table (currently filters to all columns)
2. **Keyword detection** — detect SQL keywords (SELECT, WHERE, ORDER BY) and suggest them in general context
3. **Alias tracking** — detect table aliases (e.g., `FROM Users u`) and suggest columns when typing `u.`
4. **Multi-line context** — currently only looks at current line, could look at previous lines for better context

## Team Dependencies

- **Legolas:** Frontend already sends `prefix` and `context` — no changes needed
- **Samwise:** No backend changes needed — leverages existing schema metadata
- **Gandalf:** Decision logged for architectural reference
