using System.Diagnostics;
using System.Text.RegularExpressions;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// Executes SQL queries through readonly connections with write-operation validation.
/// </summary>
public sealed partial class SqlQueryExecutor : IQueryExecutor
{
    private readonly IConnectionFactory _connectionFactory;

    // Forbidden SQL keywords that indicate write operations
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER",
        "TRUNCATE", "CREATE", "EXEC", "EXECUTE"
    ];

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WriteOperationPattern();

    public SqlQueryExecutor(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<QueryResult> ExecuteReadOnlyQueryAsync(QueryRequest request)
    {
        var validation = ValidateQuery(request.Sql);
        if (validation is not null)
            return validation;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = request.Sql;
            command.CommandTimeout = 30;

            await using var reader = await command.ExecuteReaderAsync();

            var columnNames = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
                columnNames.Add(reader.GetName(i));

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            stopwatch.Stop();

            return new QueryResult
            {
                RowCount = rows.Count,
                ColumnCount = columnNames.Count,
                ColumnNames = columnNames,
                Rows = rows,
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = true
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new QueryResult
            {
                RowCount = 0,
                ColumnCount = 0,
                ColumnNames = [],
                Rows = [],
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static QueryResult? ValidateQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new QueryResult
            {
                RowCount = 0,
                ColumnCount = 0,
                ColumnNames = [],
                ExecutionMilliseconds = 0,
                Succeeded = false,
                ErrorMessage = "Query text cannot be empty."
            };
        }

        if (WriteOperationPattern().IsMatch(sql))
        {
            return new QueryResult
            {
                RowCount = 0,
                ColumnCount = 0,
                ColumnNames = [],
                ExecutionMilliseconds = 0,
                Succeeded = false,
                ErrorMessage = "Write operations are not permitted. Only SELECT queries are allowed."
            };
        }

        return null;
    }
}
