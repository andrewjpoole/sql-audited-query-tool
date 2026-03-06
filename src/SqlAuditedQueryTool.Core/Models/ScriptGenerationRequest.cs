namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a request to generate sql-script-runner scripts for a work item.
/// </summary>
public sealed class ScriptGenerationRequest
{
    public required string Sql { get; init; }
    public required string RepositoryKey { get; init; }
    public required int WorkItemId { get; init; }
    public required string Purpose { get; init; }
    public int ExpectedAffectedRows { get; init; }
    public string? RequestedBy { get; init; }
}
