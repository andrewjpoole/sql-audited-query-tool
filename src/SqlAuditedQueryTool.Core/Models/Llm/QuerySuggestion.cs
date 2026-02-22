namespace SqlAuditedQueryTool.Core.Models.Llm;

public sealed class QuerySuggestion
{
    public required string SuggestedSql { get; init; }
    public required string Explanation { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsFixQuery { get; init; }
    public double? Confidence { get; init; }
}
