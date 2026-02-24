# Decision: SQL Keywords Added to Autocomplete Vector Store

**Date:** 2026-02-23  
**Author:** Radagast  
**Status:** Implemented

## Context

User reported that typing "SELEC" and hitting Ctrl+Space showed "no suggestions". Autocomplete should show at least SQL keywords even when there are no matching schema items.

## Problem

The `SchemaEmbeddingService` only embedded database tables and columns. SQL keywords (SELECT, FROM, WHERE, etc.) were never added to the vector store, so partial keyword matches like "SELEC" returned zero results.

## Decision

Added 50+ common T-SQL keywords to the vector store during schema embedding initialization. Keywords are:
- Stored in category "keyword" (separate from "schema" category for tables/columns)
- Always shown in autocomplete regardless of SQL context
- Embedded with text format: `"{KEYWORD} - SQL keyword"`

## Implementation

1. **SchemaEmbeddingService**: New `AddSqlKeywords()` method embeds T-SQL keywords at startup
2. **EmbeddingCompletionService**: Searches both "schema" and "keyword" categories in parallel
3. **Context filtering**: Keywords always shown, schema items filtered by SQL context (FROM → tables, SELECT → columns, etc.)

## Keywords Included

Core SQL: SELECT, FROM, WHERE, JOIN variants, GROUP BY, ORDER BY, HAVING, DISTINCT, TOP, AS  
Operators: AND, OR, NOT, IN, EXISTS, BETWEEN, LIKE, IS NULL, IS NOT NULL  
Aggregates: COUNT, SUM, AVG, MIN, MAX  
Functions: CAST, CONVERT, SUBSTRING, TRIM, UPPER, LOWER, CONCAT, COALESCE, ISNULL, GETDATE, DATEADD, DATEDIFF, YEAR, MONTH, DAY  
Advanced: UNION, EXCEPT, INTERSECT, WITH, OVER, PARTITION BY, ROW_NUMBER, RANK, DENSE_RANK

## Impact

- Typing "SELEC" now shows "SELECT" suggestion
- Autocomplete always provides SQL keyword hints
- No performance impact (keywords embedded once at startup)
- Vector store size increases by ~50 items (negligible)

## Files Modified

- `src/SqlAuditedQueryTool.Llm/Services/SchemaEmbeddingService.cs`
- `src/SqlAuditedQueryTool.Llm/Services/EmbeddingCompletionService.cs`

## Team Considerations

- **Legolas (Frontend)**: No changes needed — Monaco autocomplete will automatically receive keyword suggestions via existing `/api/completions/schema` endpoint
- **Samwise (Database)**: No impact
- **Future expansion**: Can add more keywords or SQL-specific suggestions (snippets, common patterns) using same category-based approach
