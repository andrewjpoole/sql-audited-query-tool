using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Audit;

/// <summary>
/// Posts query audit entries as comments on an Azure DevOps work item.
/// </summary>
public sealed class AzDoAuditLogger
{
    private readonly HttpClient _httpClient;
    private readonly string? _organisation;
    private readonly string? _project;
    private readonly ILogger<AzDoAuditLogger> _logger;
    private readonly bool _isConfigured;

    public AzDoAuditLogger(IConfiguration configuration, ILogger<AzDoAuditLogger> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AzDoAudit");

        var section = configuration.GetSection("AzDoAudit");
        _organisation = section["Organisation"];
        _project = section["Project"];
        var token = section["Token"];

        _isConfigured = !string.IsNullOrEmpty(_organisation)
            && !string.IsNullOrEmpty(_project)
            && !string.IsNullOrEmpty(token);

        if (_isConfigured)
        {
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            _logger.LogInformation("AzDO audit logger configured: {Org}/{Project}", _organisation, _project);
        }
        else
        {
            _logger.LogWarning("AzDO audit logger not configured. Configure AzDoAudit:Organisation, AzDoAudit:Project, and AzDoAudit:Token to enable AzDO posting.");
        }
    }

    public async Task<string?> PostAuditCommentAsync(AuditEntry entry, int workItemId)
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("AzDO not configured — skipping AzDO audit post");
            return null;
        }

        var html = FormatHtml(entry);
        var url = $"https://dev.azure.com/{_organisation}/{_project}/_apis/wit/workitems/{workItemId}/comments?api-version=7.1-preview.4";

        try
        {
            var payload = JsonSerializer.Serialize(new { text = html });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);

            var commentUrl = doc.RootElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
            _logger.LogInformation("Audit logged to AzDO work item {WorkItemId}: {Url}", workItemId, commentUrl);
            return commentUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post audit comment to AzDO work item {WorkItemId}", workItemId);
            return null;
        }
    }

    private static string FormatHtml(AuditEntry entry)
    {
        var status = entry.Succeeded ? "✅ Success" : "❌ Failed";
        var sb = new StringBuilder();
        sb.AppendLine($"<h2>Query Audit — {status}</h2>");
        sb.AppendLine($"<p><strong>User:</strong> <code>{System.Net.WebUtility.HtmlEncode(entry.RequestedBy)}</code></p>");
        sb.AppendLine($"<p><strong>Timestamp:</strong> {entry.RequestTimestamp:O}</p>");
        sb.AppendLine($"<p><strong>Execution Time:</strong> {entry.ExecutionMilliseconds}ms</p>");
        sb.AppendLine($"<p><strong>Rows Returned:</strong> {entry.RowCount}</p>");
        sb.AppendLine($"<p><strong>Columns:</strong> {entry.ColumnCount}</p>");
        sb.AppendLine("<p><strong>Query:</strong></p>");
        sb.AppendLine($"<pre><code>{System.Net.WebUtility.HtmlEncode(entry.Sql)}</code></pre>");

        if (!string.IsNullOrEmpty(entry.ErrorMessage))
        {
            sb.AppendLine($"<blockquote>⚠️ <strong>Error:</strong> {System.Net.WebUtility.HtmlEncode(entry.ErrorMessage)}</blockquote>");
        }

        sb.AppendLine($"<p><em>Integrity: <code>{entry.IntegrityHash}</code></em></p>");

        return sb.ToString();
    }
}
