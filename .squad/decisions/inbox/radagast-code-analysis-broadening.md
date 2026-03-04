# Decision: Broaden Code Analysis from EF-Only to All Database Patterns

**Date:** 2026-02-24  
**Agent:** Radagast  
**Status:** Implemented  

## Context

The LLM needed to understand database interactions beyond just Entity Framework. Applications often use Dapper for micro-ORMs or direct ADO.NET for performance-critical paths. The existing `AnalyzeEntityFrameworkContext` tool was too narrow.

## Decision

Replaced the `AnalyzeEntityFrameworkContext` LLM tool with a broader `AnalyzeCode` tool that:
1. Analyzes ALL C# classes in a directory (not just DbContext classes)
2. Detects database-related code patterns for EF Core, Dapper, and ADO.NET
3. Provides class-level summaries with technology-specific details
4. Still performs deep EF Core analysis for DbContext classes

## Rationale

- **Comprehensive coverage**: LLM can now help with queries across all database access patterns
- **Technology detection**: Automatically identifies which data access technology each class uses
- **Backward compatible**: EF Core analysis remains detailed through the embedded EfContexts property
- **Scalable pattern**: Easy to add more database technologies (e.g., NHibernate, Marten) in the future

## Implementation Approach

### Model Structure
- `CodeAnalysisResult`: Top-level result with class list and summary counts
- `ClassAnalysis`: Generic class analysis with optional database-specific details
- `DapperUsage` and `AdoNetUsage`: Technology-specific pattern details attached to ClassAnalysis
- `EntityFrameworkContext`: Detailed EF analysis embedded in result when EF classes found

### Detection Strategy
- **EF Core**: Roslyn-based inheritance check (`BaseTypes.Contains("DbContext")`)
- **Dapper**: String pattern matching for `.Query<`, `.Execute(`, `using Dapper;`
- **ADO.NET**: String pattern matching for `SqlConnection`, `SqlCommand`, `ExecuteReader`, etc.

This is intentionally simple for first iteration. Could enhance with semantic model analysis for more accurate detection.

## Alternatives Considered

1. **Separate tools for each technology** (AnalyzeEFCore, AnalyzeDapper, AnalyzeAdoNet)
   - Rejected: Would require LLM to know which tool to use, increases complexity

2. **Per-method usage tracking** (detailed line-by-line database call locations)
   - Rejected for now: Too verbose for LLM context, can add later if needed

3. **Semantic model analysis** (using Roslyn's semantic model for type resolution)
   - Rejected for now: Adds complexity and compilation requirements, string matching is "good enough"

## Impact

- LLM can now understand Dapper and ADO.NET query patterns
- LLM can suggest queries based on code inspection across all database access styles
- No breaking changes: existing EF analysis still works, just nested in broader result

## Future Enhancements

- Add NHibernate, Marten, Entity Framework 6 detection
- Use Roslyn semantic model for higher accuracy (e.g., detect Dapper extension methods on IDbConnection)
- Extract actual SQL strings from code for query suggestion context
- Detect stored procedure calls and inline SQL in all technologies
