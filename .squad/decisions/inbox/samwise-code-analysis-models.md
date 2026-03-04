# Decision: Code Analysis Model Hierarchy

**Date:** 2026-03-03  
**Decider:** Samwise (Backend Dev)  
**Status:** Implemented

## Context

Extended the code context system to support comprehensive repository analysis beyond just Entity Framework Core. The tool needed to analyze ALL application code with special attention to database-related patterns (EF Core, Dapper, ADO.NET).

## Decision

Created a **two-tier model hierarchy** for code analysis:

### Tier 1: General Class Analysis (Lightweight)
- **ClassAnalysis**: General class metadata
  - Uses **PropertySummary** (Name, Type, Attributes only)
  - Uses **MethodSummary** (Name, ReturnType, Parameters, Attributes)
  - Flags: `IsDbRelated`, `DbTechnology`

### Tier 2: EF Entity Analysis (Detailed)
- **EntityDefinition**: EF-specific entity metadata  
  - Uses **PropertyDefinition** (includes `IsKey`, `ColumnName`, `MaxLength`, etc.)
  - Includes `NavigationProperty`, `Indexes`, `Configurations`

### Usage Pattern Tracking (Detailed)
- **DapperUsage**: Track individual Dapper method calls
  - Includes: `FilePath`, `ClassName`, `MethodName`, `LineNumber`, `QueryType`, `SqlSnippet`
- **AdoNetUsage**: Track individual ADO.NET patterns
  - Includes: `FilePath`, `ClassName`, `MethodName`, `LineNumber`, `Pattern`, `SqlSnippet`

## Rationale

1. **Separation of concerns**: General classes don't need EF-specific metadata (IsKey, ColumnName, etc.)
2. **Performance**: Lightweight PropertySummary for general analysis, detailed PropertyDefinition only for EF entities
3. **Audit trail compatibility**: Detailed usage tracking (with line numbers, SQL snippets) supports audit logging requirements
4. **Backward compatibility**: All existing models remain untouched, new models compose on top

## Alternatives Considered

### Option 1: Single unified Property model (REJECTED)
- Force all classes to use PropertyDefinition with EF-specific fields
- **Rejected**: Pollutes general class analysis with irrelevant EF metadata

### Option 2: Aggregate Dapper/ADO.NET usage at class level (REJECTED)
- Store usage as lists of patterns per class (e.g., `List<string> QueryMethods`)
- **Rejected**: Loses granularity needed for audit trail (which method? which line?)

### Option 3: Minimal usage tracking without line numbers (REJECTED)
- Track only that Dapper/ADO.NET is used, not where
- **Rejected**: Doesn't support audit requirements for query source tracking

## Implementation Notes

- **CodeAnalysisResult** aggregates:
  - All classes (ClassAnalysis)
  - EF contexts (EntityFrameworkContext)
  - Dapper usages (DapperUsage list)
  - ADO.NET usages (AdoNetUsage list)
  - Directory and file count

- **ICodeContextService.AnalyzeCodeAsync()** calls existing `AnalyzeEntityFrameworkContextAsync()` internally
  - Reuses EF analysis for EntityFrameworkContexts
  - Adds comprehensive class analysis and DB usage pattern extraction

## Consequences

### Positive
✅ Clear separation between general and EF-specific analysis  
✅ Supports audit trail requirements with detailed usage tracking  
✅ Backward compatible — no breaking changes to existing consumers  
✅ Enables LLM to understand full codebase, not just EF layer  

### Negative
⚠️ Two separate property models (PropertySummary vs PropertyDefinition) — developers must choose correct one  
⚠️ CodeAnalysisResult has both flat lists (DapperUsages, AdoNetUsages) and nested structures (ClassAnalysis with references)  

## Related Files
- `src\SqlAuditedQueryTool.Core\Models\Llm\CodeContextModels.cs`
- `src\SqlAuditedQueryTool.Core\Interfaces\Llm\ICodeContextService.cs`
- `src\SqlAuditedQueryTool.Llm\Services\CodeContextService.cs` (implementation)

## Future Considerations
- Consider adding `RecordAnalysis` for C# records (currently treated as classes)
- May need `InterfaceAnalysis` if interface-based patterns become important
- Could add `AttributeUsage` tracking if attribute-driven patterns need analysis
