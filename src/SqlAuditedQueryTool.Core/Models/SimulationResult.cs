namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents the result of simulating a write query.
/// </summary>
public sealed class SimulationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int? EstimatedAffectedRows { get; init; }
    public string? ExecutionPlanXml { get; init; }
    public long ExecutionMilliseconds { get; init; }
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
}
