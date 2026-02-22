namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a query execution record with source tracking.
/// </summary>
public sealed class QueryHistory
{
    public required Guid Id { get; init; }
    public required string Sql { get; init; }
    public required string RequestedBy { get; init; }
    public required QuerySource Source { get; init; }
    public required DateTimeOffset RequestTimestamp { get; init; }
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required IReadOnlyList<string> ColumnNames { get; init; }
    public required long ExecutionMilliseconds { get; init; }
    public required bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public string? GitHubIssueUrl { get; init; }
}

/// <summary>
/// Indicates the source of a query execution.
/// </summary>
public enum QuerySource
{
    User,
    AI
}
