using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Audit;

/// <summary>
/// Posts query audit entries as comments on a configured GitHub issue.
/// </summary>
public sealed class GitHubAuditLogger : IAuditLogger
{
    private readonly IGitHubClient? _gitHubClient;
    private readonly string? _repoOwner;
    private readonly string? _repoName;
    private readonly int? _issueNumber;
    private readonly ILogger<GitHubAuditLogger> _logger;
    private readonly bool _isConfigured;

    public GitHubAuditLogger(IConfiguration configuration, ILogger<GitHubAuditLogger> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("GitHubAudit");
        _repoOwner = section["RepoOwner"];
        _repoName = section["RepoName"];
        var issueNumberStr = section["IssueNumber"];
        var token = section["Token"];

        // Check if all required config values are present
        _isConfigured = !string.IsNullOrEmpty(_repoOwner)
            && !string.IsNullOrEmpty(_repoName)
            && !string.IsNullOrEmpty(issueNumberStr)
            && !string.IsNullOrEmpty(token);

        if (_isConfigured)
        {
            _issueNumber = int.Parse(issueNumberStr!);
            _gitHubClient = new GitHubClient(new ProductHeaderValue("SqlAuditedQueryTool"))
            {
                Credentials = new Credentials(token!)
            };
            _logger.LogInformation("GitHub audit logger configured: {Repo}/{Issue}", $"{_repoOwner}/{_repoName}", _issueNumber);
        }
        else
        {
            _logger.LogWarning("GitHub audit logger not configured. Audit entries will be logged locally only. Configure GitHubAudit:RepoOwner, GitHubAudit:RepoName, GitHubAudit:IssueNumber, and GitHubAudit:Token to enable GitHub posting.");
        }
    }

    public async Task<AuditEntry> LogQueryAsync(QueryRequest request, QueryResult result)
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

        // Log locally first
        _logger.LogInformation("Query audit: User={User}, RowCount={RowCount}, ExecutionMs={Ms}, Succeeded={Success}",
            entry.RequestedBy, entry.RowCount, entry.ExecutionMilliseconds, entry.Succeeded);

        // Only post to GitHub if configured
        if (_isConfigured && _gitHubClient is not null)
        {
            var markdown = FormatMarkdown(entry);

            try
            {
                var comment = await _gitHubClient.Issue.Comment.Create(
                    _repoOwner!, _repoName!, _issueNumber!.Value, markdown);

                entry.GitHubIssueUrl = comment.HtmlUrl;
                _logger.LogInformation("Audit logged to GitHub: {Url}", comment.HtmlUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post audit comment to GitHub issue");
                // Audit failure should not block the query result
            }
        }
        else
        {
            _logger.LogWarning("GitHub not configured — audit entry logged locally only");
        }

        return entry;
    }

    private static string FormatMarkdown(AuditEntry entry)
    {
        var status = entry.Succeeded ? "✅ Success" : "❌ Failed";
        var sb = new StringBuilder();
        sb.AppendLine($"## Query Audit — {status}");
        sb.AppendLine();
        sb.AppendLine($"**User:** `{entry.RequestedBy}`");
        sb.AppendLine($"**Timestamp:** {entry.RequestTimestamp:O}");
        sb.AppendLine($"**Execution Time:** {entry.ExecutionMilliseconds}ms");
        sb.AppendLine($"**Rows Returned:** {entry.RowCount}");
        sb.AppendLine($"**Columns:** {entry.ColumnCount}");
        sb.AppendLine();
        sb.AppendLine("**Query:**");
        sb.AppendLine("```sql");
        sb.AppendLine(entry.Sql);
        sb.AppendLine("```");

        if (!string.IsNullOrEmpty(entry.ErrorMessage))
        {
            sb.AppendLine();
            sb.AppendLine($"> ⚠️ **Error:** {entry.ErrorMessage}");
        }

        sb.AppendLine();
        sb.AppendLine($"*Integrity: `{entry.IntegrityHash}`*");

        return sb.ToString();
    }

    private static string ComputeHash(QueryRequest request, QueryResult result)
    {
        var input = $"{request.Sql}|{request.RequestedBy}|{request.Timestamp:O}|{result.RowCount}|{result.ExecutionMilliseconds}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes)[..16];
    }
}
