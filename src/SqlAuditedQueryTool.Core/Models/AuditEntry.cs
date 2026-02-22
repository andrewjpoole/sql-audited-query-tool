namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents an audit record stored as a GitHub issue.
/// </summary>
public sealed class AuditEntry
{
    public required string Sql { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTimeOffset RequestTimestamp { get; init; }
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required IReadOnlyList<string> ColumnNames { get; init; }
    public required long ExecutionMilliseconds { get; init; }
    public required bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset ResultTimestamp { get; init; }
    public required string IntegrityHash { get; init; }
    public string? GitHubIssueUrl { get; set; }
}
