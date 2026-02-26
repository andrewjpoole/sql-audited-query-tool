namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Execution plan mode for query requests.
/// </summary>
public enum ExecutionPlanMode
{
    /// <summary>No execution plan requested.</summary>
    None = 0,
    /// <summary>Estimated plan only - query is NOT executed.</summary>
    Estimated = 1,
    /// <summary>Actual plan - query is executed with statistics.</summary>
    Actual = 2
}

/// <summary>
/// Represents a SQL query request entering the pipeline.
/// </summary>
public sealed class QueryRequest
{
    public required string Sql { get; init; }
    public required string RequestedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ConnectionAlias { get; init; }
    public ExecutionPlanMode ExecutionPlanMode { get; init; } = ExecutionPlanMode.None;
}
