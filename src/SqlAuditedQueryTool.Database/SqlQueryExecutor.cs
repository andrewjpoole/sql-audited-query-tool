using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// Executes SQL queries through readonly connections with write-operation validation.
/// </summary>
public sealed partial class SqlQueryExecutor : IQueryExecutor
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<SqlQueryExecutor> _logger;

    // Forbidden SQL keywords that indicate write operations
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER",
        "TRUNCATE", "CREATE", "EXEC", "EXECUTE"
    ];

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WriteOperationPattern();

    public SqlQueryExecutor(IConnectionFactory connectionFactory, ILogger<SqlQueryExecutor> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
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
            
            string? executionPlanXml = null;
            var resultSets = new List<QueryResultSet>();
            
            // For Estimated mode, we need to execute SET SHOWPLAN_XML ON in a separate batch
            if (request.ExecutionPlanMode == ExecutionPlanMode.Estimated)
            {
                // Execute SET SHOWPLAN_XML ON
                await using var setPlanCommand = connection.CreateCommand();
                setPlanCommand.CommandText = "SET SHOWPLAN_XML ON";
                setPlanCommand.CommandTimeout = 30;
                await setPlanCommand.ExecuteNonQueryAsync();
                
                // Now execute the query - it will return the plan XML instead of results
                await using (var queryCommand = connection.CreateCommand())
                {
                    queryCommand.CommandText = request.Sql;
                    queryCommand.CommandTimeout = 30;
                    
                    await using (var reader = await queryCommand.ExecuteReaderAsync())
                    {
                        // Read the plan XML (should be single row, single column)
                        if (await reader.ReadAsync() && reader.FieldCount > 0)
                        {
                            var planValue = reader.GetValue(0);
                            if (planValue is string xmlContent)
                            {
                                executionPlanXml = xmlContent;
                                _logger.LogInformation("Estimated execution plan captured: {XmlLength} characters", executionPlanXml.Length);
                            }
                        }
                    } // Reader is now closed
                } // Command is now disposed
                
                // Turn off SHOWPLAN_XML for future commands on this connection
                await using var unsetPlanCommand = connection.CreateCommand();
                unsetPlanCommand.CommandText = "SET SHOWPLAN_XML OFF";
                unsetPlanCommand.CommandTimeout = 30;
                await unsetPlanCommand.ExecuteNonQueryAsync();
            }
            else
            {
                // For Actual mode or None mode, use the existing logic
                await using var command = connection.CreateCommand();
                
                command.CommandText = request.ExecutionPlanMode == ExecutionPlanMode.Actual
                    ? $"SET STATISTICS XML ON;\n{request.Sql}\nSET STATISTICS XML OFF;"
                    : request.Sql;
                
                command.CommandTimeout = 30;

                await using var reader = await command.ExecuteReaderAsync();

                var resultSetIndex = 0;

                do
                {
                    resultSetIndex++;
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

                    // Detect execution plan result set (for Actual mode)
                    // Plan appears as last result set: 1 row, 1 column, XML starting with <ShowPlanXML
                    if (request.ExecutionPlanMode == ExecutionPlanMode.Actual && 
                        rows.Count >= 1 && 
                        reader.FieldCount == 1)
                    {
                        // Check if first row contains XML plan
                        var firstRowValue = rows[0].Values.FirstOrDefault();
                        if (firstRowValue is string xmlContent && 
                            xmlContent.TrimStart().StartsWith("<ShowPlanXML", StringComparison.OrdinalIgnoreCase))
                        {
                            executionPlanXml = xmlContent;
                            _logger.LogInformation("Actual execution plan captured: {XmlLength} characters", executionPlanXml.Length);
                            // Skip adding this result set to the collection
                            continue;
                        }
                    }

                    var resultSet = new QueryResultSet
                    {
                        RowCount = rows.Count,
                        ColumnCount = columnNames.Count,
                        ColumnNames = columnNames,
                        Rows = rows
                    };
                    
                    resultSets.Add(resultSet);
                    _logger.LogInformation("Result set {Index}: {RowCount} rows, {ColumnCount} columns", 
                        resultSetIndex, resultSet.RowCount, resultSet.ColumnCount);

                } while (await reader.NextResultAsync());
            }

            stopwatch.Stop();

            _logger.LogInformation("Query executed successfully: {ResultSetCount} result set(s), {TotalRows} total rows, {ExecutionMs}ms, HasPlan={HasPlan}",
                resultSets.Count, resultSets.Sum(rs => rs.RowCount), stopwatch.ElapsedMilliseconds, executionPlanXml != null);

            return new QueryResult
            {
                ResultSets = resultSets,
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = true,
                ExecutionPlanXml = executionPlanXml
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Query execution failed after {ExecutionMs}ms", stopwatch.ElapsedMilliseconds);
            
            string errorMessage = ex.Message;
            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                // Build detailed error message with line number and error number
                errorMessage = $"{sqlEx.Message}";
                if (sqlEx.LineNumber > 0)
                {
                    errorMessage += $" (Line {sqlEx.LineNumber})";
                }
                if (sqlEx.Number > 0)
                {
                    errorMessage += $" [Error {sqlEx.Number}]";
                }
            }
            
            return new QueryResult
            {
                ResultSets = [],
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private static QueryResult? ValidateQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new QueryResult
            {
                ResultSets = [],
                ExecutionMilliseconds = 0,
                Succeeded = false,
                ErrorMessage = "Query text cannot be empty."
            };
        }

        if (WriteOperationPattern().IsMatch(sql))
        {
            return new QueryResult
            {
                ResultSets = [],
                ExecutionMilliseconds = 0,
                Succeeded = false,
                ErrorMessage = "Write operations are not permitted. Only SELECT queries are allowed."
            };
        }

        return null;
    }
}
