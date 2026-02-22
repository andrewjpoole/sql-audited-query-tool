namespace SqlAuditedQueryTool.Core.Models.Llm;

public sealed class LlmResponse
{
    public required string Text { get; init; }
    public List<SuggestedQuery> SuggestedQueries { get; init; } = [];
    public List<ToolCallRequest> ToolCalls { get; init; } = [];
    public double? Confidence { get; init; }
}

public sealed class SuggestedQuery
{
    public required string Sql { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsFixQuery { get; init; }
}
