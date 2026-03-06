using System.Text.RegularExpressions;
using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Llm.Services;

/// <summary>
/// Lightweight SQL schema validator that checks LLM-generated queries against known schema.
/// Uses basic regex parsing — not a full SQL parser.
/// </summary>
public static class SqlSchemaValidator
{
    /// <summary>
    /// Validates a SQL query against the known schema, returning warnings for unrecognized
    /// table or column references. Includes "did you mean?" fuzzy suggestions.
    /// </summary>
    public static List<string> Validate(string sql, SchemaContext schema)
    {
        if (string.IsNullOrWhiteSpace(sql) || schema?.Tables is not { Count: > 0 })
            return [];

        var warnings = new List<string>();
        var knownTables = BuildTableLookup(schema);

        // Strip comments so regex patterns don't match keywords inside comments
        var strippedSql = SqlHelper.StripSqlComments(sql);

        var referencedTables = ExtractTableReferences(strippedSql);
        foreach (var tableRef in referencedTables)
        {
            var resolved = ResolveTable(tableRef, knownTables);
            if (resolved == null)
            {
                var suggestion = FindClosestMatch(tableRef.TableName, knownTables.Keys);
                var hint = suggestion != null ? $" — did you mean '{suggestion}'?" : "";
                warnings.Add($"Table '{tableRef.TableName}' not found in schema{hint}");
            }
        }

        var referencedColumns = ExtractColumnReferences(strippedSql);
        foreach (var colRef in referencedColumns)
        {
            ValidateColumnReference(colRef, referencedTables, knownTables, warnings);
        }

        return warnings;
    }

    private static Dictionary<string, TableSchema> BuildTableLookup(SchemaContext schema)
    {
        var lookup = new Dictionary<string, TableSchema>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in schema.Tables)
        {
            // Register by table name and schema-qualified name
            lookup.TryAdd(table.TableName, table);
            lookup.TryAdd($"{table.SchemaName}.{table.TableName}", table);
        }
        return lookup;
    }

    internal static List<TableReference> ExtractTableReferences(string sql)
    {
        var tables = new List<TableReference>();
        // Match FROM/JOIN/INTO/UPDATE/DELETE FROM followed by table reference
        // Handles: [schema].[table], schema.table, [table], table
        // Also handles table aliases (e.g., FROM Users u, FROM Users AS u)
        var pattern = new Regex(
            @"(?:FROM|JOIN|INTO|UPDATE|MERGE\s+INTO?)\s+" +
            @"(?:\[?(\w+)\]?\.)?" +       // optional schema
            @"\[?(\w+)\]?" +               // table name
            @"(?:\s+(?:AS\s+)?(\w+))?",    // optional alias
            RegexOptions.IgnoreCase);

        foreach (Match match in pattern.Matches(sql))
        {
            var schemaName = match.Groups[1].Success ? match.Groups[1].Value : null;
            var tableName = match.Groups[2].Value;
            var alias = match.Groups[3].Success ? match.Groups[3].Value : null;

            // Skip SQL keywords that might be captured as aliases
            if (IsKeyword(tableName)) continue;

            tables.Add(new TableReference(schemaName, tableName, alias));
        }

        return tables;
    }

    internal static List<ColumnReference> ExtractColumnReferences(string sql)
    {
        var columns = new List<ColumnReference>();
        // Match table.column or alias.column patterns (e.g., u.Name, Users.Id, [dbo].[Users].[Name])
        var qualifiedPattern = new Regex(
            @"(?<!\w)\[?(\w+)\]?\.\[?(\w+)\]?(?!\.\w)(?=\s*[,\s=<>!+\-*/)|;\r\n])",
            RegexOptions.IgnoreCase);

        foreach (Match match in qualifiedPattern.Matches(sql))
        {
            var tableOrAlias = match.Groups[1].Value;
            var columnName = match.Groups[2].Value;

            // Skip schema.table patterns (they'll be handled by table extraction)
            if (IsKeyword(tableOrAlias) || IsKeyword(columnName)) continue;
            // Skip known SQL functions and common false positives
            if (IsFunctionOrType(columnName)) continue;

            columns.Add(new ColumnReference(tableOrAlias, columnName));
        }

        return columns;
    }

    private static TableSchema? ResolveTable(TableReference tableRef, Dictionary<string, TableSchema> knownTables)
    {
        if (tableRef.SchemaName != null)
        {
            var qualified = $"{tableRef.SchemaName}.{tableRef.TableName}";
            if (knownTables.TryGetValue(qualified, out var t)) return t;
        }
        return knownTables.GetValueOrDefault(tableRef.TableName);
    }

    private static void ValidateColumnReference(
        ColumnReference colRef,
        List<TableReference> referencedTables,
        Dictionary<string, TableSchema> knownTables,
        List<string> warnings)
    {
        // Resolve the table/alias to a known table
        var table = ResolveColumnTable(colRef.TableOrAlias, referencedTables, knownTables);
        if (table == null)
            return; // Can't validate — table not found (already warned about that)

        var columnExists = table.Columns.Any(c =>
            string.Equals(c.ColumnName, colRef.ColumnName, StringComparison.OrdinalIgnoreCase));

        if (!columnExists)
        {
            var columnNames = table.Columns.Select(c => c.ColumnName).ToList();
            var suggestion = FindClosestMatch(colRef.ColumnName, columnNames);
            var hint = suggestion != null ? $" — did you mean '{suggestion}'?" : "";
            warnings.Add($"Column '{colRef.ColumnName}' not found in table '{table.TableName}'{hint}");
        }
    }

    private static TableSchema? ResolveColumnTable(
        string tableOrAlias,
        List<TableReference> referencedTables,
        Dictionary<string, TableSchema> knownTables)
    {
        // Check if it's a direct table name
        if (knownTables.TryGetValue(tableOrAlias, out var directTable))
            return directTable;

        // Check if it's an alias
        var aliasMatch = referencedTables.FirstOrDefault(t =>
            string.Equals(t.Alias, tableOrAlias, StringComparison.OrdinalIgnoreCase));

        if (aliasMatch != null)
            return knownTables.GetValueOrDefault(aliasMatch.TableName);

        return null;
    }

    internal static string? FindClosestMatch(string input, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        var maxDistance = Math.Max(2, input.Length / 3); // Allow up to ~33% character errors

        foreach (var candidate in candidates)
        {
            // Quick prefix/contains check
            if (candidate.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                input.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            var distance = LevenshteinDistance(input.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (distance < bestDistance && distance <= maxDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    internal static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var costs = new int[b.Length + 1];
        for (var i = 0; i <= b.Length; i++) costs[i] = i;

        for (var i = 1; i <= a.Length; i++)
        {
            var prev = costs[0];
            costs[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var temp = costs[j];
                costs[j] = a[i - 1] == b[j - 1]
                    ? prev
                    : 1 + Math.Min(Math.Min(costs[j], costs[j - 1]), prev);
                prev = temp;
            }
        }

        return costs[b.Length];
    }

    private static bool IsKeyword(string word)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "ON", "AS",
            "JOIN", "INNER", "LEFT", "RIGHT", "OUTER", "CROSS", "FULL",
            "ORDER", "BY", "GROUP", "HAVING", "UNION", "ALL", "DISTINCT",
            "TOP", "SET", "INTO", "VALUES", "INSERT", "UPDATE", "DELETE",
            "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "WITH",
            "EXISTS", "BETWEEN", "LIKE", "IS", "NULL", "CASE", "WHEN",
            "THEN", "ELSE", "END", "ASC", "DESC", "LIMIT", "OFFSET",
            "EXEC", "EXECUTE", "DECLARE", "BEGIN", "COMMIT", "ROLLBACK",
            "MERGE", "USING", "MATCHED", "OUTPUT", "OVER", "PARTITION",
            "ROWS", "RANGE", "PRECEDING", "FOLLOWING", "UNBOUNDED", "CURRENT"
        };
        return keywords.Contains(word);
    }

    private static bool IsFunctionOrType(string word)
    {
        var functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "COUNT", "SUM", "AVG", "MIN", "MAX", "ROW_NUMBER", "RANK",
            "DENSE_RANK", "NTILE", "LAG", "LEAD", "FIRST_VALUE", "LAST_VALUE",
            "GETDATE", "GETUTCDATE", "DATEADD", "DATEDIFF", "DATEPART",
            "LEN", "SUBSTRING", "REPLACE", "TRIM", "LTRIM", "RTRIM",
            "CAST", "CONVERT", "ISNULL", "COALESCE", "NULLIF",
            "UPPER", "LOWER", "LEFT", "RIGHT", "CHARINDEX", "STUFF",
            "INT", "VARCHAR", "NVARCHAR", "BIT", "DATETIME", "DATE",
            "FLOAT", "DECIMAL", "BIGINT", "SMALLINT", "TINYINT", "UNIQUEIDENTIFIER",
            "VALUE", "KEY", "TYPE", "NAME", "STATUS", "TEXT"
        };
        return functions.Contains(word);
    }

    internal record TableReference(string? SchemaName, string TableName, string? Alias);
    internal record ColumnReference(string TableOrAlias, string ColumnName);
}
