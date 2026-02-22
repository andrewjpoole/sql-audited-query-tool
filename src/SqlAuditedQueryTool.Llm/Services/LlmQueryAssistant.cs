using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Llm.Services;

public sealed class LlmQueryAssistant : IQueryAssistant
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmQueryAssistant> _logger;

    public LlmQueryAssistant(ILlmService llmService, ILogger<LlmQueryAssistant> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<QuerySuggestion> SuggestQueryAsync(
        string naturalLanguageRequest,
        SchemaContext schema,
        CancellationToken cancellationToken = default)
    {
        var schemaDescription = FormatSchemaForPrompt(schema);
        var prompt =
            $"Given this database schema:\n{schemaDescription}\n\n" +
            $"Write a SQL query to: {naturalLanguageRequest}\n\n" +
            "Only use SELECT statements for read queries. If the user needs a fix " +
            "(INSERT/UPDATE/DELETE), clearly label it as '-- FIX QUERY' at the top of the SQL " +
            "that must be run separately with proper authorization.\n\n" +
            "Return the SQL query wrapped in ```sql code blocks, followed by a brief explanation.";

        var request = new LlmChatRequest
        {
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
            SchemaContext = schema
        };

        _logger.LogDebug("Requesting query suggestion for: {Request}", naturalLanguageRequest);

        var response = await _llmService.ChatAsync(request, cancellationToken);
        return ParseQuerySuggestion(response);
    }

    private static QuerySuggestion ParseQuerySuggestion(LlmResponse response)
    {
        var text = response.Text;
        var sql = string.Empty;
        var explanation = text;
        var isFixQuery = false;

        // Extract SQL from code block
        var codeBlockPattern = new System.Text.RegularExpressions.Regex(
            @"```sql\s*\n(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var match = codeBlockPattern.Match(text);
        if (match.Success)
        {
            sql = match.Groups[1].Value.Trim();
            // Explanation is everything outside the code block
            explanation = text[..match.Index].Trim();
            var afterBlock = text[(match.Index + match.Length)..].Trim();
            if (!string.IsNullOrEmpty(afterBlock))
            {
                explanation = string.IsNullOrEmpty(explanation)
                    ? afterBlock
                    : $"{explanation}\n{afterBlock}";
            }
        }

        if (!string.IsNullOrEmpty(sql))
        {
            var sqlUpper = sql.TrimStart().ToUpperInvariant();
            isFixQuery = sqlUpper.StartsWith("INSERT") || sqlUpper.StartsWith("UPDATE") ||
                         sqlUpper.StartsWith("DELETE") || sqlUpper.StartsWith("ALTER") ||
                         sqlUpper.StartsWith("DROP") || sqlUpper.StartsWith("CREATE") ||
                         sqlUpper.StartsWith("EXEC") || sqlUpper.StartsWith("MERGE") ||
                         sql.Contains("-- FIX QUERY", StringComparison.OrdinalIgnoreCase) ||
                         text.Contains("FIX QUERY", StringComparison.OrdinalIgnoreCase);
        }

        return new QuerySuggestion
        {
            SuggestedSql = sql,
            Explanation = explanation,
            IsReadOnly = !isFixQuery,
            IsFixQuery = isFixQuery,
            Confidence = response.Confidence
        };
    }

    private static string FormatSchemaForPrompt(SchemaContext schema)
    {
        // SAFETY: Only table/column metadata — never row data
        var sb = new System.Text.StringBuilder();
        foreach (var table in schema.Tables)
        {
            sb.AppendLine($"Table: [{table.SchemaName}].[{table.TableName}]");
            sb.AppendLine("Columns:");
            foreach (var col in table.Columns)
            {
                var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                var maxLen = col.MaxLength.HasValue ? $"({col.MaxLength})" : "";
                sb.AppendLine($"  {col.ColumnName} {col.DataType}{maxLen} {nullable}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
