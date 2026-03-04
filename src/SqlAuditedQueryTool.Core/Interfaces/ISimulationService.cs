using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Interfaces;

/// <summary>
/// Service for simulating write queries without executing them.
/// </summary>
public interface ISimulationService
{
    Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken = default);
}
