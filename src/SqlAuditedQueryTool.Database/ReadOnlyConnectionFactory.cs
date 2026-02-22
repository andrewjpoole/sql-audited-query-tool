using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SqlAuditedQueryTool.Core.Interfaces;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// Creates readonly SQL Server connections with ApplicationIntent=ReadOnly
/// and READ UNCOMMITTED isolation to prevent blocking production workloads.
/// </summary>
public sealed class ReadOnlyConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;

    public ReadOnlyConnectionFactory(IConfiguration configuration)
    {
        var baseConnectionString = configuration.GetConnectionString("db")
            ?? throw new InvalidOperationException("Connection string 'db' is not configured.");

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            ApplicationIntent = ApplicationIntent.ReadOnly
        };

        _connectionString = builder.ConnectionString;
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Enforce readonly isolation — no locks, no blocking production
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
