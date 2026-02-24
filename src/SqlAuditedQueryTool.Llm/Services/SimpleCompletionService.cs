using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using System.Text.RegularExpressions;

namespace SqlAuditedQueryTool.Llm.Services;

public class SimpleCompletionService : ICompletionService
{
    private readonly ISchemaProvider _schemaProvider;
    private static readonly string[] SqlKeywords = new[]
    {
        "SELECT", "*", "FROM", "WHERE", "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "OUTER",
        "ON", "AND", "OR", "NOT", "IN", "LIKE", "BETWEEN", "IS", "NULL", "AS",
        "ORDER BY", "GROUP BY", "HAVING", "DISTINCT", "TOP", "LIMIT", "OFFSET",
        "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP", "TRUNCATE"
    };

    public SimpleCompletionService(ISchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    public async Task<IReadOnlyList<CompletionItem>> GetSchemaCompletionsAsync(
        CompletionContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.Prefix))
            return Array.Empty<CompletionItem>();

        var schema = await _schemaProvider.GetSchemaAsync(ct);
        var sqlContext = DetectSqlContext(context.Prefix);
        var items = new List<CompletionItem>();

        switch (sqlContext)
        {
            case SqlContextType.AfterFrom:
            case SqlContextType.AfterJoin:
                // Only tables, no keywords
                foreach (var table in schema.Tables)
                {
                    var fullName = $"{table.SchemaName}.{table.TableName}";
                    items.Add(new CompletionItem
                    {
                        Label = fullName,
                        InsertText = fullName,
                        Kind = "Field",
                        Detail = $"Table ({table.Columns.Count} columns)",
                        Documentation = $"Columns: {string.Join(", ", table.Columns.Take(5).Select(c => c.ColumnName))}"
                    });
                }
                break;

            case SqlContextType.AfterSelect:
            case SqlContextType.AfterWhere:
                // Columns from all tables
                foreach (var table in schema.Tables)
                {
                    foreach (var column in table.Columns)
                    {
                        var qualifiedName = $"{table.SchemaName}.{table.TableName}.{column.ColumnName}";
                        items.Add(new CompletionItem
                        {
                            Label = qualifiedName,
                            InsertText = qualifiedName,
                            Kind = "Field",
                            Detail = column.DataType,
                            Documentation = $"Table: {table.SchemaName}.{table.TableName}"
                        });
                    }
                }
                // Also add keywords for SELECT context
                if (sqlContext == SqlContextType.AfterSelect)
                {
                    foreach (var keyword in SqlKeywords)
                    {
                        items.Add(new CompletionItem
                        {
                            Label = keyword,
                            InsertText = keyword,
                            Kind = "Keyword",
                            Detail = "SQL Keyword"
                        });
                    }
                }
                break;

            case SqlContextType.General:
                // All: tables, columns, keywords
                foreach (var table in schema.Tables)
                {
                    var fullName = $"{table.SchemaName}.{table.TableName}";
                    items.Add(new CompletionItem
                    {
                        Label = fullName,
                        InsertText = fullName,
                        Kind = "Field",
                        Detail = "Table"
                    });
                }
                foreach (var keyword in SqlKeywords)
                {
                    items.Add(new CompletionItem
                    {
                        Label = keyword,
                        InsertText = keyword,
                        Kind = "Keyword",
                        Detail = "SQL Keyword"
                    });
                }
                break;
        }

        return items;
    }

    private static SqlContextType DetectSqlContext(string prefix)
    {
        var normalized = prefix.ToUpperInvariant();

        // Check for FROM/JOIN - want table names
        if (Regex.IsMatch(normalized, @"\bFROM\s+[\w.]*$", RegexOptions.IgnoreCase))
            return SqlContextType.AfterFrom;

        if (Regex.IsMatch(normalized, @"\b(INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN)\s+[\w.]*$", RegexOptions.IgnoreCase))
            return SqlContextType.AfterJoin;

        // Check for SELECT - want columns and keywords
        if (Regex.IsMatch(normalized, @"\bSELECT\s+\w*$", RegexOptions.IgnoreCase))
            return SqlContextType.AfterSelect;

        // Check for WHERE/AND/OR - want columns
        if (Regex.IsMatch(normalized, @"\b(WHERE|AND|OR)\s+\w*$", RegexOptions.IgnoreCase))
            return SqlContextType.AfterWhere;

        return SqlContextType.General;
    }

    private enum SqlContextType
    {
        General,
        AfterSelect,
        AfterFrom,
        AfterJoin,
        AfterWhere
    }
}
