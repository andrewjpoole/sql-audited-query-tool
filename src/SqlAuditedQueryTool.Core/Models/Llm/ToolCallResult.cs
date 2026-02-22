namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Represents the result of a tool execution to be sent back to the LLM.
/// </summary>
public sealed class ToolCallResult
{
    public required string ToolCallId { get; init; }
    public required string Result { get; init; }
    public bool IsError { get; init; }
}
