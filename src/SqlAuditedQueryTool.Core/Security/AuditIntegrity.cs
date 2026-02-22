using System.Security.Cryptography;
using System.Text;
using SqlAuditedQueryTool.Core.Models;

namespace SqlAuditedQueryTool.Core.Security;

/// <summary>
/// Provides tamper-detection hashing for audit entries.
/// </summary>
public static class AuditIntegrity
{
    /// <summary>
    /// Generates a SHA-256 hash over the query request and result metadata
    /// to create a tamper-evident seal for the audit record.
    /// </summary>
    public static string GenerateAuditHash(QueryRequest request, QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var payload = BuildCanonicalPayload(
            request.Sql,
            request.RequestedBy,
            request.Timestamp,
            result.RowCount,
            result.ColumnCount,
            result.ColumnNames,
            result.ExecutionMilliseconds,
            result.Succeeded,
            result.ErrorMessage,
            result.Timestamp);

        return ComputeSha256(payload);
    }

    /// <summary>
    /// Verifies that an audit entry's integrity hash matches the expected value.
    /// Returns true if the entry has not been tampered with.
    /// </summary>
    public static bool VerifyAuditHash(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var payload = BuildCanonicalPayload(
            entry.Sql,
            entry.RequestedBy,
            entry.RequestTimestamp,
            entry.RowCount,
            entry.ColumnCount,
            entry.ColumnNames,
            entry.ExecutionMilliseconds,
            entry.Succeeded,
            entry.ErrorMessage,
            entry.ResultTimestamp);

        var expectedHash = ComputeSha256(payload);
        return string.Equals(entry.IntegrityHash, expectedHash, StringComparison.Ordinal);
    }

    private static string BuildCanonicalPayload(
        string sql,
        string requestedBy,
        DateTimeOffset requestTimestamp,
        int rowCount,
        int columnCount,
        IReadOnlyList<string> columnNames,
        long executionMilliseconds,
        bool succeeded,
        string? errorMessage,
        DateTimeOffset resultTimestamp)
    {
        var sb = new StringBuilder();
        sb.Append("SQL:").AppendLine(sql);
        sb.Append("BY:").AppendLine(requestedBy);
        sb.Append("REQ_TS:").AppendLine(requestTimestamp.ToString("O"));
        sb.Append("ROWS:").AppendLine(rowCount.ToString());
        sb.Append("COLS:").AppendLine(columnCount.ToString());
        sb.Append("COL_NAMES:").AppendLine(string.Join(",", columnNames));
        sb.Append("EXEC_MS:").AppendLine(executionMilliseconds.ToString());
        sb.Append("OK:").AppendLine(succeeded.ToString());
        sb.Append("ERR:").AppendLine(errorMessage ?? "");
        sb.Append("RES_TS:").AppendLine(resultTimestamp.ToString("O"));
        return sb.ToString();
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
