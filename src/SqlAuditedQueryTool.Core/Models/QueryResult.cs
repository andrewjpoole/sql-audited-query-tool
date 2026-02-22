namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents the result of a query execution including row data.
/// </summary>
public sealed class QueryResult
{
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required IReadOnlyList<string> ColumnNames { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
    public required long ExecutionMilliseconds { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
