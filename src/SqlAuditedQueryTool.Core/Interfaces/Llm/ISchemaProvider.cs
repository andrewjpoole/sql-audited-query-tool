using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

/// <summary>
/// Provides database schema metadata for LLM context.
/// SAFETY: Returns ONLY table/column names and types — never row data.
/// </summary>
public interface ISchemaProvider
{
    Task<SchemaContext> GetSchemaAsync(CancellationToken cancellationToken = default);
}
