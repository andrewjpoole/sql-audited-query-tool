using SqlAuditedQueryTool.Core.Models;
using SqlAuditedQueryTool.Core.Security;

namespace SqlAuditedQueryTool.Core.Tests.Security;

public class AuditIntegrityTests
{
    private static readonly DateTimeOffset FixedRequestTime =
        new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset FixedResultTime =
        new(2026, 1, 15, 10, 30, 1, TimeSpan.Zero);

    private QueryRequest CreateRequest(string sql = "SELECT * FROM Users") => new()
    {
        Sql = sql,
        RequestedBy = "TestUser",
        Timestamp = FixedRequestTime
    };

    private QueryResult CreateResult() => new()
    {
        RowCount = 10,
        ColumnCount = 3,
        ColumnNames = ["Id", "Name", "Email"],
        ExecutionMilliseconds = 42,
        Succeeded = true,
        Timestamp = FixedResultTime
    };

    // ── Deterministic hash generation ────────────────────────────

    [Fact]
    public void GenerateAuditHash_IsDeterministic()
    {
        var request = CreateRequest();
        var result = CreateResult();

        var hash1 = AuditIntegrity.GenerateAuditHash(request, result);
        var hash2 = AuditIntegrity.GenerateAuditHash(request, result);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GenerateAuditHash_ProducesValidHexString()
    {
        var hash = AuditIntegrity.GenerateAuditHash(CreateRequest(), CreateResult());

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void GenerateAuditHash_DifferentInputsProduceDifferentHashes()
    {
        var result = CreateResult();
        var hash1 = AuditIntegrity.GenerateAuditHash(CreateRequest("SELECT 1"), result);
        var hash2 = AuditIntegrity.GenerateAuditHash(CreateRequest("SELECT 2"), result);

        Assert.NotEqual(hash1, hash2);
    }

    // ── Hash verification ────────────────────────────────────────

    [Fact]
    public void VerifyAuditHash_ReturnsTrueForValidEntry()
    {
        var request = CreateRequest();
        var result = CreateResult();
        var hash = AuditIntegrity.GenerateAuditHash(request, result);

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
            IntegrityHash = hash
        };

        Assert.True(AuditIntegrity.VerifyAuditHash(entry));
    }

    [Fact]
    public void VerifyAuditHash_CatchesTamperedSql()
    {
        var request = CreateRequest();
        var result = CreateResult();
        var hash = AuditIntegrity.GenerateAuditHash(request, result);

        var entry = new AuditEntry
        {
            Sql = "SELECT * FROM Secrets",
            RequestedBy = request.RequestedBy,
            RequestTimestamp = request.Timestamp,
            RowCount = result.RowCount,
            ColumnCount = result.ColumnCount,
            ColumnNames = result.ColumnNames,
            ExecutionMilliseconds = result.ExecutionMilliseconds,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            ResultTimestamp = result.Timestamp,
            IntegrityHash = hash
        };

        Assert.False(AuditIntegrity.VerifyAuditHash(entry));
    }

    [Fact]
    public void VerifyAuditHash_CatchesTamperedRowCount()
    {
        var request = CreateRequest();
        var result = CreateResult();
        var hash = AuditIntegrity.GenerateAuditHash(request, result);

        var entry = new AuditEntry
        {
            Sql = request.Sql,
            RequestedBy = request.RequestedBy,
            RequestTimestamp = request.Timestamp,
            RowCount = 999,
            ColumnCount = result.ColumnCount,
            ColumnNames = result.ColumnNames,
            ExecutionMilliseconds = result.ExecutionMilliseconds,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            ResultTimestamp = result.Timestamp,
            IntegrityHash = hash
        };

        Assert.False(AuditIntegrity.VerifyAuditHash(entry));
    }

    [Fact]
    public void VerifyAuditHash_CatchesTamperedHash()
    {
        var request = CreateRequest();
        var result = CreateResult();

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
            IntegrityHash = "deadbeef00000000000000000000000000000000000000000000000000000000"
        };

        Assert.False(AuditIntegrity.VerifyAuditHash(entry));
    }

    // ── Null guard ───────────────────────────────────────────────

    [Fact]
    public void GenerateAuditHash_ThrowsOnNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AuditIntegrity.GenerateAuditHash(null!, CreateResult()));
    }

    [Fact]
    public void GenerateAuditHash_ThrowsOnNullResult()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AuditIntegrity.GenerateAuditHash(CreateRequest(), null!));
    }

    [Fact]
    public void VerifyAuditHash_ThrowsOnNullEntry()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AuditIntegrity.VerifyAuditHash(null!));
    }
}
