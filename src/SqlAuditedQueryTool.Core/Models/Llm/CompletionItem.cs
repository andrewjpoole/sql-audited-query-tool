namespace SqlAuditedQueryTool.Core.Models.Llm;

public class CompletionItem
{
    public required string Label { get; init; }
    public required string InsertText { get; init; }
    public required string Kind { get; init; }
    public string? Detail { get; init; }
    public string? Documentation { get; init; }
    public float Score { get; init; }
}
