namespace SqlAuditedQueryTool.Core.Models;

/// <summary>
/// Represents a request to simulate a write query without executing it.
/// </summary>
public sealed class SimulationRequest
{
    public required string Sql { get; init; }
    public string? RequestedBy { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
