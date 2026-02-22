namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Represents a request from the LLM to execute a tool.
/// </summary>
public sealed class ToolCallRequest
{
    public required string ToolCallId { get; init; }
    public required string ToolName { get; init; }
    public required Dictionary<string, object?> Arguments { get; init; }
}
