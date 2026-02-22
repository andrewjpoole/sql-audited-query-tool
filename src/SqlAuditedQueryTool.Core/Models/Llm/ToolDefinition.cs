namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Defines a tool that can be called by the LLM.
/// </summary>
public sealed class ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Dictionary<string, ToolParameter> Parameters { get; init; }
}

/// <summary>
/// Defines a parameter for a tool.
/// </summary>
public sealed class ToolParameter
{
    public required string Type { get; init; }
    public required string Description { get; init; }
    public bool Required { get; init; }
}
