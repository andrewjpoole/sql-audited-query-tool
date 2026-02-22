using SqlAuditedQueryTool.Core.Security;

namespace SqlAuditedQueryTool.Core.Tests.Security;

public class DataLeakPreventionTests
{
    // ── Allows schema metadata ───────────────────────────────────

    [Fact]
    public void ValidateLlmPayload_AllowsSchemaMetadata()
    {
        var payload = new
        {
            Tables = new[]
            {
                new { Name = "Users", Columns = new[] { "Id", "Name" } },
                new { Name = "Orders", Columns = new[] { "Id", "Total" } }
            }
        };

        Assert.True(DataLeakPrevention.ValidateLlmPayload(payload));
    }

    [Fact]
    public void ValidateLlmPayload_AllowsColumnTypes()
    {
        var payload = new
        {
            TableName = "Products",
            Columns = new[]
            {
                new { Name = "Id", Type = "int" },
                new { Name = "Name", Type = "nvarchar" },
                new { Name = "Price", Type = "decimal" }
            }
        };

        Assert.True(DataLeakPrevention.ValidateLlmPayload(payload));
    }

    // ── Blocks payloads with PII patterns ────────────────────────

    [Fact]
    public void ValidateLlmPayload_BlocksEmailAddresses()
    {
        var payload = new { Data = "Contact: alice@example.com" };
        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "PII_EMAIL");
    }

    [Fact]
    public void ValidateLlmPayload_BlocksSsnPatterns()
    {
        var payload = new { Value = "SSN: 123-45-6789" };
        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "PII_SSN");
    }

    [Fact]
    public void ValidateLlmPayload_BlocksPhoneNumbers()
    {
        var payload = new { Phone = "(555) 123-4567" };
        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "PII_PHONE");
    }

    [Fact]
    public void ValidateLlmPayload_BlocksCreditCardNumbers()
    {
        var payload = new { Card = "4111-1111-1111-1111" };
        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "PII_CREDIT_CARD");
    }

    [Fact]
    public void ValidateLlmPayload_BlocksGuidRowIds()
    {
        var payload = new { RowId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890" };
        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "POTENTIAL_ROW_ID");
    }

    // ── Blocks large string arrays (row data) ───────────────────

    [Fact]
    public void ValidateLlmPayload_BlocksLargeStringArrays()
    {
        var payload = new
        {
            Values = new[] { "Alice", "Bob", "Charlie", "Diana", "Edward" }
        };

        Assert.False(DataLeakPrevention.ValidateLlmPayload(payload));

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.Contains(report.Violations, v => v.Rule == "ROW_DATA_ARRAY");
    }

    [Fact]
    public void ValidateLlmPayload_AllowsSmallStringArrays()
    {
        var payload = new
        {
            Columns = new[] { "Id", "Name", "Type" }
        };

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.DoesNotContain(report.Violations, v => v.Rule == "ROW_DATA_ARRAY");
    }

    // ── Null payload ─────────────────────────────────────────────

    [Fact]
    public void ValidateLlmPayload_AllowsNull()
    {
        Assert.True(DataLeakPrevention.ValidateLlmPayload(null));
    }

    // ── Nested PII detection ─────────────────────────────────────

    [Fact]
    public void InspectPayload_FindsPiiInNestedObjects()
    {
        var payload = new
        {
            Level1 = new
            {
                Level2 = new
                {
                    Email = "hidden@deep.com"
                }
            }
        };

        var report = DataLeakPrevention.InspectPayload(payload);
        Assert.False(report.IsClean);
        Assert.Contains(report.Violations, v => v.Rule == "PII_EMAIL");
    }
}
