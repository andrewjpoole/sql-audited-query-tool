using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Core.Interfaces.Llm;

/// <summary>
/// Generates SQL query suggestions from natural language using the local LLM.
/// Only schema metadata is sent to the LLM — never row data.
/// </summary>
public interface IQueryAssistant
{
    Task<QuerySuggestion> SuggestQueryAsync(string naturalLanguageRequest, SchemaContext schema, CancellationToken cancellationToken = default);
}
