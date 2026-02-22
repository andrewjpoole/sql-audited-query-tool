using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Configuration;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace SqlAuditedQueryTool.Llm.Services;

public sealed class OllamaLlmService : ILlmService
{
    internal const string DefaultSystemPrompt =
        "You are a SQL Server query assistant for incident investigation. " +
        "You help investigate incidents by executing queries and analyzing results. " +
        "Use the execute_sql_query tool to run SELECT queries when needed. " +
        "After seeing results, provide analysis and suggest follow-up queries if helpful.";

    private readonly IChatClient _client;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaLlmService> _logger;
    private readonly IQueryExecutor? _queryExecutor;

    public OllamaLlmService(
        IChatClient client, 
        IOptions<OllamaOptions> options, 
        ILogger<OllamaLlmService> logger,
        IQueryExecutor? queryExecutor = null)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _queryExecutor = queryExecutor;
    }

    public async Task<LlmResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(request);
        // TODO: Tool calling requires Ollama model with function calling support and proper integration
        // Current Microsoft.Extensions.AI may not fully support Ollama tool calling yet
        // Keeping infrastructure in place for when support is available

        _logger.LogDebug("Sending chat request to Ollama model {Model}", _options.Model);

        var response = await _client.GetResponseAsync(messages, cancellationToken: cancellationToken);
        
        // Extract tool calls if present (currently returns empty list)
        var toolCalls = ExtractToolCalls(response);
        
        var text = response.Text ?? string.Empty;

        return new LlmResponse
        {
            Text = text,
            SuggestedQueries = ParseSuggestedQueries(text),
            ToolCalls = toolCalls
        };
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        LlmChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(request);

        await foreach (var update in _client.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
        {
            if (update.Text is { Length: > 0 } content)
                yield return content;
        }
    }

    private static List<AIChatMessage> BuildMessages(LlmChatRequest request)
    {
        var messages = new List<AIChatMessage>();

        var systemPrompt = request.SystemPrompt ?? DefaultSystemPrompt;
        if (request.SchemaContext is { Tables.Count: > 0 } schema)
        {
            systemPrompt += "\n\nAvailable database schema (metadata only — no row data):\n" + FormatSchema(schema);
        }
        messages.Add(new AIChatMessage(ChatRole.System, systemPrompt));

        foreach (var msg in request.Messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "system" => ChatRole.System,
                "assistant" => ChatRole.Assistant,
                _ => ChatRole.User
            };
            messages.Add(new AIChatMessage(role, msg.Content));
        }

        return messages;
    }

    private static string FormatSchema(SchemaContext schema)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var table in schema.Tables)
        {
            sb.AppendLine($"[{table.SchemaName}].[{table.TableName}]");
            foreach (var col in table.Columns)
            {
                var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                var maxLen = col.MaxLength.HasValue ? $"({col.MaxLength})" : "";
                sb.AppendLine($"  - {col.ColumnName} {col.DataType}{maxLen} {nullable}");
            }
        }
        return sb.ToString();
    }

    internal static List<SuggestedQuery> ParseSuggestedQueries(string text)
    {
        var queries = new List<SuggestedQuery>();
        var codeBlockPattern = new System.Text.RegularExpressions.Regex(
            @"```sql\s*\n(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in codeBlockPattern.Matches(text))
        {
            var sql = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(sql)) continue;

            var isFixQuery = IsFixQuery(sql, text, match.Index);
            queries.Add(new SuggestedQuery
            {
                Sql = sql,
                IsReadOnly = !isFixQuery,
                IsFixQuery = isFixQuery
            });
        }

        return queries;
    }

    private static bool IsFixQuery(string sql, string fullText, int matchIndex)
    {
        var sqlUpper = sql.TrimStart().ToUpperInvariant();
        if (sqlUpper.StartsWith("INSERT") || sqlUpper.StartsWith("UPDATE") ||
            sqlUpper.StartsWith("DELETE") || sqlUpper.StartsWith("ALTER") ||
            sqlUpper.StartsWith("DROP") || sqlUpper.StartsWith("CREATE") ||
            sqlUpper.StartsWith("EXEC") || sqlUpper.StartsWith("MERGE"))
        {
            return true;
        }

        // Check if preceded by "FIX QUERY" label
        var preceding = fullText[..matchIndex];
        return preceding.Contains("FIX QUERY", StringComparison.OrdinalIgnoreCase);
    }

    // TODO: Enable when Ollama tool calling is properly supported
    /*
    private static List<AITool> BuildTools()
    {
        return
        [
            AIFunctionFactory.Create(
                (string sql, string reason) => new { sql, reason },
                name: "execute_sql_query",
                description: "Execute a readonly SQL SELECT query against the database and return results for analysis")
        ];
    }
    */

    private List<ToolCallRequest> ExtractToolCalls(ChatResponse response)
    {
        var toolCalls = new List<ToolCallRequest>();
        
        // For now, we'll check if the response itself contains function call content
        // ChatResponse may directly expose tool call information
        // If Ollama doesn't support tools natively, we may need to parse from text
        
        // Return empty list for now - tool calling may need different integration approach
        return toolCalls;
    }

    public async Task<string> ExecuteToolCallAsync(ToolCallRequest toolCall, CancellationToken cancellationToken = default)
    {
        if (_queryExecutor == null)
        {
            return "Error: Query executor not available";
        }

        switch (toolCall.ToolName)
        {
            case "execute_sql_query":
                return await ExecuteSqlQueryToolAsync(toolCall.Arguments, cancellationToken);
            
            default:
                return $"Error: Unknown tool '{toolCall.ToolName}'";
        }
    }

    private async Task<string> ExecuteSqlQueryToolAsync(Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        if (!args.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
        {
            return "Error: Missing or invalid 'sql' parameter";
        }

        var reason = args.TryGetValue("reason", out var reasonObj) && reasonObj is string r ? r : "LLM investigation";

        try
        {
            var queryRequest = new QueryRequest
            {
                Sql = sql,
                RequestedBy = "Ollama"
            };

            var result = await _queryExecutor!.ExecuteReadOnlyQueryAsync(queryRequest);

            if (!result.Succeeded)
            {
                return $"Query failed: {result.ErrorMessage}";
            }

            // Format results for LLM analysis
            return FormatQueryResultsForLlm(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool call execute_sql_query failed");
            return $"Error executing query: {ex.Message}";
        }
    }

    private static string FormatQueryResultsForLlm(QueryResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Query executed successfully. Returned {result.RowCount} rows in {result.ExecutionMilliseconds}ms.");
        sb.AppendLine();
        sb.AppendLine("Columns: " + string.Join(", ", result.ColumnNames));
        sb.AppendLine();
        
        if (result.RowCount > 0)
        {
            sb.AppendLine("Sample rows (up to 10):");
            var sampleRows = result.Rows.Take(10);
            foreach (var row in sampleRows)
            {
                sb.Append("  ");
                foreach (var col in result.ColumnNames)
                {
                    var value = row.TryGetValue(col, out var v) ? v : null;
                    sb.Append($"{col}={value ?? "NULL"}, ");
                }
                sb.AppendLine();
            }
            
            if (result.RowCount > 10)
            {
                sb.AppendLine($"  ... and {result.RowCount - 10} more rows");
            }
        }

        return sb.ToString();
    }

}
