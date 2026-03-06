using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Audit;

/// <summary>
/// Composite audit logger that builds the AuditEntry and delegates posting
/// to GitHub and Azure DevOps loggers based on per-request IDs.
/// </summary>
public sealed class CompositeAuditLogger : IAuditLogger
{
    private readonly GitHubAuditLogger _gitHubLogger;
    private readonly AzDoAuditLogger _azDoLogger;
    private readonly ILogger<CompositeAuditLogger> _logger;

    public CompositeAuditLogger(
        GitHubAuditLogger gitHubLogger,
        AzDoAuditLogger azDoLogger,
        ILogger<CompositeAuditLogger> logger)
    {
        _gitHubLogger = gitHubLogger;
        _azDoLogger = azDoLogger;
        _logger = logger;
    }

    public async Task<AuditEntry> LogQueryAsync(QueryRequest request, QueryResult result, int? gitHubIssueNumber = null, int? azDoWorkItemId = null)
    {
        var entry = new AuditEntry
        {
            Sql = request.Sql,
            RequestedBy = request.RequestedBy,
            RequestTimestamp = request.Timestamp,
            RowCount = result.RowCount,
            ColumnCount = result.ColumnCount,
            ColumnNames = result.ColumnNames,
            ExecutionMilliseconds = result.ExecutionMilliseconds,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            ResultTimestamp = result.Timestamp,
            IntegrityHash = ComputeHash(request, result)
        };

        _logger.LogInformation("Query audit: User={User}, RowCount={RowCount}, ExecutionMs={Ms}, Succeeded={Success}",
            entry.RequestedBy, entry.RowCount, entry.ExecutionMilliseconds, entry.Succeeded);

        // Skip external audit trail posting for failed queries
        if (!result.Succeeded)
        {
            _logger.LogInformation("Query failed — audit entry logged locally only (not posted to external audit trail)");
            return entry;
        }

        // Post to GitHub if issue number supplied
        if (gitHubIssueNumber.HasValue)
        {
            entry.GitHubIssueUrl = await _gitHubLogger.PostAuditCommentAsync(entry, gitHubIssueNumber.Value);
        }

        // Post to AzDO if work item ID supplied
        if (azDoWorkItemId.HasValue)
        {
            entry.AzDoWorkItemUrl = await _azDoLogger.PostAuditCommentAsync(entry, azDoWorkItemId.Value);
        }

        if (!gitHubIssueNumber.HasValue && !azDoWorkItemId.HasValue)
        {
            _logger.LogInformation("No GitHub issue or AzDO work item specified — audit entry logged locally only");
        }

        return entry;
    }

    private static string ComputeHash(QueryRequest request, QueryResult result)
    {
        var input = $"{request.Sql}|{request.RequestedBy}|{request.Timestamp:O}|{result.RowCount}|{result.ExecutionMilliseconds}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes)[..16];
    }
}
