using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Database;

/// <summary>
/// Service for simulating write queries using SHOWPLAN_XML without executing them.
/// </summary>
public sealed partial class SimulationService : ISimulationService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILogger<SimulationService> _logger;

    // Forbidden operations for write simulations
    [GeneratedRegex(@"\b(DROP|TRUNCATE|ALTER|CREATE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ForbiddenOperationsPattern();

    // Write operations that should have WHERE clauses
    [GeneratedRegex(@"\b(UPDATE|DELETE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WriteOperationsPattern();

    // Check if WHERE clause exists
    [GeneratedRegex(@"\bWHERE\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WhereClausePattern();

    public SimulationService(IConnectionFactory connectionFactory, ILogger<SimulationService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<SimulationResult> SimulateAsync(SimulationRequest request, CancellationToken cancellationToken = default)
    {
        var validationErrors = new List<string>();
        var warnings = new List<string>();

        // Validate SQL is not empty
        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            validationErrors.Add("Query text cannot be empty.");
        }

        // Check for forbidden operations
        if (ForbiddenOperationsPattern().IsMatch(request.Sql))
        {
            validationErrors.Add("Forbidden operations detected (DROP, TRUNCATE, ALTER, CREATE, EXEC, EXECUTE). Only UPDATE, INSERT, DELETE are allowed for simulation.");
        }

        // Check for missing WHERE clause on UPDATE/DELETE
        if (WriteOperationsPattern().IsMatch(request.Sql) && !WhereClausePattern().IsMatch(request.Sql))
        {
            warnings.Add("UPDATE or DELETE statement without WHERE clause detected. This may affect all rows in the table.");
        }

        // If validation failed, return early
        if (validationErrors.Count > 0)
        {
            return new SimulationResult
            {
                IsValid = false,
                ValidationErrors = validationErrors,
                Warnings = warnings,
                Succeeded = false
            };
        }

        var stopwatch = Stopwatch.StartNew();
        string? executionPlanXml = null;
        int? estimatedAffectedRows = null;

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            // Execute SET SHOWPLAN_XML ON
            await using var setPlanCommand = connection.CreateCommand();
            setPlanCommand.CommandText = "SET SHOWPLAN_XML ON";
            setPlanCommand.CommandTimeout = 30;
            await setPlanCommand.ExecuteNonQueryAsync(cancellationToken);

            // Execute the query - returns the execution plan XML without executing the statement
            await using (var queryCommand = connection.CreateCommand())
            {
                queryCommand.CommandText = request.Sql;
                queryCommand.CommandTimeout = 30;

                await using (var reader = await queryCommand.ExecuteReaderAsync(cancellationToken))
                {
                    // Read the plan XML (should be single row, single column)
                    if (await reader.ReadAsync(cancellationToken) && reader.FieldCount > 0)
                    {
                        var planValue = reader.GetValue(0);
                        if (planValue is string xmlContent)
                        {
                            executionPlanXml = xmlContent;
                            _logger.LogInformation("Execution plan captured: {XmlLength} characters", executionPlanXml.Length);
                        }
                    }
                }
            }

            // Turn off SHOWPLAN_XML
            await using var unsetPlanCommand = connection.CreateCommand();
            unsetPlanCommand.CommandText = "SET SHOWPLAN_XML OFF";
            unsetPlanCommand.CommandTimeout = 30;
            await unsetPlanCommand.ExecuteNonQueryAsync(cancellationToken);

            // Parse estimated rows from execution plan XML
            if (!string.IsNullOrEmpty(executionPlanXml))
            {
                try
                {
                    var xdoc = XDocument.Parse(executionPlanXml);
                    XNamespace ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";
                    var rootRelOp = xdoc.Descendants(ns + "RelOp").FirstOrDefault();
                    if (rootRelOp != null)
                    {
                        var estimateRows = rootRelOp.Attribute("EstimateRows")?.Value;
                        if (double.TryParse(estimateRows, out var rows))
                        {
                            estimatedAffectedRows = (int)Math.Round(rows);
                            _logger.LogInformation("Estimated affected rows: {Rows}", estimatedAffectedRows);

                            // Warn if estimated rows > 100
                            if (estimatedAffectedRows > 100)
                            {
                                warnings.Add($"High estimated affected rows: {estimatedAffectedRows}. Please verify this is intended.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse estimated rows from execution plan XML");
                }
            }

            stopwatch.Stop();

            return new SimulationResult
            {
                IsValid = true,
                ValidationErrors = [],
                Warnings = warnings,
                EstimatedAffectedRows = estimatedAffectedRows,
                ExecutionPlanXml = executionPlanXml,
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = true
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Simulation failed after {ExecutionMs}ms", stopwatch.ElapsedMilliseconds);

            string errorMessage = ex.Message;
            if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
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

            return new SimulationResult
            {
                IsValid = validationErrors.Count == 0,
                ValidationErrors = validationErrors,
                Warnings = warnings,
                ExecutionMilliseconds = stopwatch.ElapsedMilliseconds,
                Succeeded = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
