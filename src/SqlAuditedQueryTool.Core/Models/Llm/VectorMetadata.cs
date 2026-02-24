namespace SqlAuditedQueryTool.Core.Models.Llm;

public class VectorMetadata
{
    public required string Category { get; init; }
    public required string DisplayText { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
