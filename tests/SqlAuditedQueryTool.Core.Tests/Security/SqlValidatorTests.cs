using SqlAuditedQueryTool.Core.Security;

namespace SqlAuditedQueryTool.Core.Tests.Security;

public class SqlValidatorTests
{
    // ── Blocks write operations ──────────────────────────────────

    [Theory]
    [InlineData("INSERT INTO Users (Name) VALUES ('Alice')")]
    [InlineData("UPDATE Users SET Name = 'Bob'")]
    [InlineData("DELETE FROM Users WHERE Id = 1")]
    [InlineData("DROP TABLE Users")]
    [InlineData("ALTER TABLE Users ADD Email nvarchar(255)")]
    [InlineData("TRUNCATE TABLE Users")]
    [InlineData("CREATE TABLE Evil (Id INT)")]
    public void ValidateReadOnly_BlocksWriteOperations(string sql)
    {
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
        Assert.NotEmpty(result.Violations);
    }

    // ── Blocks EXEC / EXECUTE and stored procedure calls ─────────

    [Theory]
    [InlineData("EXEC sp_executesql N'SELECT 1'")]
    [InlineData("EXECUTE sp_who2")]
    [InlineData("EXEC xp_cmdshell 'dir'")]
    [InlineData("exec sp_helpdb")]
    public void ValidateReadOnly_BlocksExecAndStoredProcs(string sql)
    {
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    // ── Allows clean SELECT queries ──────────────────────────────

    [Theory]
    [InlineData("SELECT * FROM Users")]
    [InlineData("SELECT TOP 10 Name, Email FROM Customers ORDER BY Name")]
    [InlineData("SELECT COUNT(*) FROM Orders WHERE Status = 'Active'")]
    [InlineData("SELECT u.Name, o.Total FROM Users u JOIN Orders o ON u.Id = o.UserId")]
    [InlineData("SELECT 1")]
    public void ValidateReadOnly_AllowsCleanSelects(string sql)
    {
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.True(result.IsValid);
        Assert.Equal(RiskLevel.Safe, result.RiskLevel);
    }

    // ── Handles comments hiding write operations ─────────────────

    [Fact]
    public void ValidateReadOnly_DetectsWriteHiddenInBlockComment()
    {
        var sql = "SELECT 1; /* harmless comment */ DROP TABLE Users";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    [Fact]
    public void ValidateReadOnly_DetectsWriteAfterSingleLineComment()
    {
        var sql = "SELECT 1 -- just a query\nDROP TABLE Users";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    [Fact]
    public void ValidateReadOnly_CommentHidingSelect_StillBlocksDrop()
    {
        var sql = "/* SELECT * FROM Users */ DROP TABLE Users";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
    }

    // ── Write keywords inside string literals should NOT block ───

    [Fact]
    public void ValidateReadOnly_AllowsWriteKeywordsInsideStringLiterals()
    {
        var sql = "SELECT * FROM Logs WHERE Message = 'INSERT failed on UPDATE'";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateReadOnly_AllowsDeleteKeywordInStringLiteral()
    {
        var sql = "SELECT * FROM AuditLog WHERE Action = 'DELETE'";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateReadOnly_AllowsDropInsideEscapedStringLiteral()
    {
        var sql = "SELECT * FROM Logs WHERE Msg = 'He said ''DROP TABLE'' as a joke'";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.True(result.IsValid);
    }

    // ── UNION-based injection detection ──────────────────────────

    [Fact]
    public void ValidateReadOnly_FlagsUnionAsSuspicious()
    {
        var sql = "SELECT Name FROM Users UNION SELECT Password FROM Credentials";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.True(result.IsValid);
        Assert.Equal(RiskLevel.Suspicious, result.RiskLevel);
        Assert.Contains(result.Violations, v => v.Contains("UNION"));
    }

    // ── Multi-statement batches (semicolons) ─────────────────────

    [Fact]
    public void ValidateReadOnly_FlagsMultiStatementBatch()
    {
        var sql = "SELECT 1; SELECT 2";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.Contains(result.Violations, v => v.Contains("Multi-statement"));
    }

    [Fact]
    public void ValidateReadOnly_BlocksMultiStatementWithWrite()
    {
        var sql = "SELECT 1; DROP TABLE Users";
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    // ── Empty / null input ───────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateReadOnly_RejectsEmptyOrNull(string? sql)
    {
        var result = SqlValidator.ValidateReadOnly(sql!);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    // ── Case insensitivity ───────────────────────────────────────

    [Theory]
    [InlineData("insert into Users values (1)")]
    [InlineData("Insert Into Users Values (1)")]
    [InlineData("InSeRt InTo Users Values (1)")]
    public void ValidateReadOnly_CaseInsensitive(string sql)
    {
        var result = SqlValidator.ValidateReadOnly(sql);
        Assert.False(result.IsValid);
        Assert.Equal(RiskLevel.Blocked, result.RiskLevel);
    }

    // ── SanitizeForAudit ─────────────────────────────────────────

    [Fact]
    public void SanitizeForAudit_RedactsPasswordPatterns()
    {
        var sql = "SELECT * FROM Config WHERE Key = 'password=SuperSecret123'";
        var sanitized = SqlValidator.SanitizeForAudit(sql);
        Assert.DoesNotContain("SuperSecret123", sanitized);
        Assert.Contains("REDACTED", sanitized);
    }

    [Fact]
    public void SanitizeForAudit_RedactsConnectionStringPatterns()
    {
        var sql = "-- pwd=MyP@ss;token=abc123";
        var sanitized = SqlValidator.SanitizeForAudit(sql);
        Assert.DoesNotContain("MyP@ss", sanitized);
        Assert.DoesNotContain("abc123", sanitized);
    }

    [Fact]
    public void SanitizeForAudit_LeavesCleanSqlUntouched()
    {
        var sql = "SELECT Name, Email FROM Users WHERE Active = 1";
        var sanitized = SqlValidator.SanitizeForAudit(sql);
        Assert.Equal(sql, sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SanitizeForAudit_HandlesNullOrEmpty(string? sql)
    {
        var sanitized = SqlValidator.SanitizeForAudit(sql!);
        Assert.Equal(sql, sanitized);
    }
}
