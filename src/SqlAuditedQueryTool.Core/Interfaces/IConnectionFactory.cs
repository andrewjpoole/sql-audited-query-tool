using System.Data.Common;

namespace SqlAuditedQueryTool.Core.Interfaces;

/// <summary>
/// Creates database connections. Implementations should return readonly connections
/// for query operations.
/// </summary>
public interface IConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
