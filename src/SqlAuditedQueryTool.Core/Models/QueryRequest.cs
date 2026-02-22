namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a SQL query request entering the pipeline.
/// </summary>
public sealed class QueryRequest
{
    public required string Sql { get; init; }
    public required string RequestedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ConnectionAlias { get; init; }
}
