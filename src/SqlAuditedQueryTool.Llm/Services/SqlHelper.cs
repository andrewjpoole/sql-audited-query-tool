using System.Text.RegularExpressions;

namespace SqlAuditedQueryTool.Llm.Services;

/// <summary>
/// Shared SQL string utilities.
/// </summary>
public static partial class SqlHelper
{
    /// <summary>
    /// Strips single-line (-- ...) and multi-line (/* ... */) comments from SQL text.
    /// </summary>
    public static string StripSqlComments(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return sql;

        return SqlCommentPattern().Replace(sql, match =>
        {
            // Preserve string literals — don't strip comments inside quotes
            if (match.Value.StartsWith("'"))
                return match.Value;

            // Replace comment with a space to avoid accidentally joining tokens
            return " ";
        }).Trim();
    }

    // Matches: single-quoted strings (to skip them), block comments, or line comments.
    [GeneratedRegex(@"'(?:[^']|'')*'|/\*[\s\S]*?\*/|--[^\r\n]*", RegexOptions.Compiled)]
    private static partial Regex SqlCommentPattern();
}
