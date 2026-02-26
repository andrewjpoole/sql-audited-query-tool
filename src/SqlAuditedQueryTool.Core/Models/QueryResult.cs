namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a single result set from a query.
/// </summary>
public sealed class QueryResultSet
{
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required IReadOnlyList<string> ColumnNames { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
}

/// <summary>
/// Represents the result of a query execution including row data.
/// </summary>
public sealed class QueryResult
{
    public required IReadOnlyList<QueryResultSet> ResultSets { get; init; }
    public required long ExecutionMilliseconds { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? ExecutionPlanXml { get; init; }
    public bool HasExecutionPlan => ExecutionPlanXml is not null;
    
    // Legacy properties for backward compatibility
    public int RowCount => ResultSets.Sum(rs => rs.RowCount);
    public int ColumnCount => ResultSets.FirstOrDefault()?.ColumnCount ?? 0;
    public IReadOnlyList<string> ColumnNames => ResultSets.FirstOrDefault()?.ColumnNames ?? [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows => ResultSets.FirstOrDefault()?.Rows ?? [];
}
