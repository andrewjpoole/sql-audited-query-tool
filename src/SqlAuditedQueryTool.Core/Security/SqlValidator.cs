using System.Text.RegularExpressions;

namespace SqlAuditedQueryTool.Core.Security;

public enum RiskLevel
{
    Safe,
    Suspicious,
    Blocked
}

public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Violations { get; init; } = [];
    public RiskLevel RiskLevel { get; init; }
}

/// <summary>
/// Validates SQL statements to enforce readonly access.
/// Strips comments and string literals before keyword detection
/// so that write keywords hidden inside comments or quoted text
/// do not produce false positives or false negatives.
/// </summary>
public static class SqlValidator
{
    // Write operation keywords that must never appear in executable SQL
    private static readonly string[] BlockedKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "CREATE", "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY"
    ];

    // Stored procedure prefixes (dangerous system procs)
    private static readonly string[] BlockedProcPrefixes = ["sp_", "xp_"];

    // Regex to match SQL single-line comments: -- to end of line
    private static readonly Regex SingleLineComment = new(@"--[^\r\n]*", RegexOptions.Compiled);

    // Regex to match SQL block comments: /* ... */ (non-greedy, supports multiline)
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

    // Regex to match SQL string literals: '...' (handles escaped quotes '')
    private static readonly Regex StringLiteral = new(@"'(?:[^']|'')*'", RegexOptions.Compiled);

    // Semicolons indicate multi-statement batches
    private static readonly Regex Semicolon = new(@";", RegexOptions.Compiled);

    /// <summary>
    /// Validates that SQL is readonly. Returns a result describing any violations.
    /// </summary>
    public static ValidationResult ValidateReadOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new ValidationResult
            {
                IsValid = false,
                Violations = ["SQL statement is empty."],
                RiskLevel = RiskLevel.Blocked
            };
        }

        var violations = new List<string>();
        var riskLevel = RiskLevel.Safe;

        // Strip string literals first (so keywords inside strings don't trigger)
        var stripped = StringLiteral.Replace(sql, " '' ");
        // Strip block comments (could hide write operations)
        stripped = BlockComment.Replace(stripped, " ");
        // Strip single-line comments
        stripped = SingleLineComment.Replace(stripped, " ");

        // Normalise whitespace for matching
        var normalised = Regex.Replace(stripped, @"\s+", " ").Trim().ToUpperInvariant();

        // Check for blocked keywords as whole words
        foreach (var keyword in BlockedKeywords)
        {
            var pattern = $@"\b{keyword}\b";
            if (Regex.IsMatch(normalised, pattern))
            {
                violations.Add($"Blocked keyword detected: {keyword}");
                riskLevel = RiskLevel.Blocked;
            }
        }

        // Check for dangerous stored procedure prefixes
        foreach (var prefix in BlockedProcPrefixes)
        {
            var pattern = $@"\b{Regex.Escape(prefix.ToUpperInvariant())}\w+";
            if (Regex.IsMatch(normalised, pattern))
            {
                violations.Add($"Stored procedure call detected: {prefix}*");
                riskLevel = RiskLevel.Blocked;
            }
        }

        // Check for multi-statement batches (semicolons separating statements)
        if (Semicolon.IsMatch(stripped))
        {
            // Check if there's meaningful SQL after the semicolon
            var parts = Semicolon.Split(stripped)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (parts.Count > 1)
            {
                violations.Add("Multi-statement batch detected (semicolons).");
                if (riskLevel < RiskLevel.Suspicious)
                    riskLevel = RiskLevel.Suspicious;
            }
        }

        // Check for UNION which can be used for injection
        if (Regex.IsMatch(normalised, @"\bUNION\b"))
        {
            if (riskLevel < RiskLevel.Suspicious)
                riskLevel = RiskLevel.Suspicious;
            // UNION in a SELECT is not inherently a violation, but is suspicious
            violations.Add("UNION detected — review for injection risk.");
        }

        return new ValidationResult
        {
            IsValid = riskLevel != RiskLevel.Blocked,
            Violations = violations,
            RiskLevel = riskLevel
        };
    }

    /// <summary>
    /// Redacts sensitive patterns from SQL before writing to audit logs.
    /// </summary>
    public static string SanitizeForAudit(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        var sanitized = sql;

        // Redact connection-string-like patterns (key=value with sensitive keys)
        sanitized = Regex.Replace(sanitized,
            @"(?i)(password|pwd|secret|token|key|connectionstring)\s*=\s*[^\s;'""]+",
            "$1=***REDACTED***");

        // Redact quoted password values
        sanitized = Regex.Replace(sanitized,
            @"(?i)(password|pwd|secret|token|key)\s*=\s*'[^']*'",
            "$1='***REDACTED***'");

        return sanitized;
    }
}
