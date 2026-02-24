using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Services;
using Moq;

namespace SqlAuditedQueryTool.Llm.Tests;

public class SimpleCompletionServiceTests
{
    private readonly Mock<ISchemaProvider> _mockSchemaProvider;
    private readonly SimpleCompletionService _service;
    private readonly SchemaContext _testSchema;

    public SimpleCompletionServiceTests()
    {
        _mockSchemaProvider = new Mock<ISchemaProvider>();
        _service = new SimpleCompletionService(_mockSchemaProvider.Object);

        // Setup test schema
        _testSchema = new SchemaContext
        {
            Tables = new List<TableSchema>
            {
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "Accounts",
                    Columns = new List<ColumnSchema>
                    {
                        new ColumnSchema { ColumnName = "AccountId", DataType = "int" },
                        new ColumnSchema { ColumnName = "AccountName", DataType = "nvarchar" }
                    }
                },
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "Users",
                    Columns = new List<ColumnSchema>
                    {
                        new ColumnSchema { ColumnName = "UserId", DataType = "int" },
                        new ColumnSchema { ColumnName = "UserName", DataType = "nvarchar" }
                    }
                },
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "AuditLog",
                    Columns = new List<ColumnSchema>
                    {
                        new ColumnSchema { ColumnName = "LogId", DataType = "int" },
                        new ColumnSchema { ColumnName = "Message", DataType = "nvarchar" }
                    }
                },
                new TableSchema
                {
                    SchemaName = "reporting",
                    TableName = "Summary",
                    Columns = new List<ColumnSchema>
                    {
                        new ColumnSchema { ColumnName = "SummaryId", DataType = "int" }
                    }
                }
            }
        };

        _mockSchemaProvider.Setup(x => x.GetSchemaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_testSchema);
    }

    #region Context Detection Tests

    [Fact]
    public async Task AfterFrom_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should return only tables
        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.Contains(result, c => c.Label == "dbo.Users");
        Assert.Contains(result, c => c.Label == "reporting.Summary");
        // Should NOT include keywords
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterJoin_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM A INNER JOIN ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should return only tables
        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        // Should NOT include keywords
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterLeftJoin_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM A LEFT JOIN ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterSelect_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should include columns
        Assert.Contains(result, c => c.Label.Contains("AccountId"));
        // Should include keywords
        Assert.Contains(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterWhere_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM Accounts WHERE ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should include columns
        Assert.Contains(result, c => c.Label.Contains("AccountId"));
        // Should NOT include keywords for WHERE context
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterAnd_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM A WHERE X = 1 AND ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should include columns (same as WHERE)
        Assert.Contains(result, c => c.Label.Contains("AccountId"));
    }

    [Fact]
    public async Task AfterOr_DetectsContextCorrectly()
    {
        var context = new CompletionContext("SELECT * FROM A WHERE X = 1 OR ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should include columns (same as WHERE)
        Assert.Contains(result, c => c.Label.Contains("AccountId"));
    }

    #endregion

    #region Table Filtering Tests

    [Fact]
    public async Task AfterFrom_ReturnsAllTables()
    {
        var context = new CompletionContext("SELECT * FROM ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // All 4 tables should be present
        Assert.Equal(4, result.Count(c => c.Kind == "Field" && c.Detail != null && c.Detail.Contains("Table")));
        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.Contains(result, c => c.Label == "dbo.Users");
        Assert.Contains(result, c => c.Label == "dbo.AuditLog");
        Assert.Contains(result, c => c.Label == "reporting.Summary");
    }

    [Fact]
    public async Task AfterJoin_ReturnsAllTables()
    {
        var context = new CompletionContext("SELECT * FROM A JOIN ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // All 4 tables should be present
        Assert.Equal(4, result.Count);
    }

    #endregion

    #region Column Filtering Tests

    [Fact]
    public async Task AfterSelect_ReturnsAllColumns()
    {
        var context = new CompletionContext("SELECT ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should have columns from all tables
        Assert.Contains(result, c => c.Label.Contains("dbo.Accounts.AccountId"));
        Assert.Contains(result, c => c.Label.Contains("dbo.Accounts.AccountName"));
        Assert.Contains(result, c => c.Label.Contains("dbo.Users.UserId"));
        Assert.Contains(result, c => c.Label.Contains("dbo.Users.UserName"));
    }

    [Fact]
    public async Task AfterWhere_ReturnsAllColumns()
    {
        var context = new CompletionContext("SELECT * FROM A WHERE ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should have columns from all tables
        Assert.Contains(result, c => c.Label.Contains("AccountId"));
        Assert.Contains(result, c => c.Label.Contains("UserId"));
    }

    [Fact]
    public async Task AfterSelect_ColumnsHaveCorrectMetadata()
    {
        var context = new CompletionContext("SELECT ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        var accountIdColumn = result.FirstOrDefault(c => c.Label.Contains("dbo.Accounts.AccountId"));
        Assert.NotNull(accountIdColumn);
        Assert.Equal("Field", accountIdColumn.Kind);
        Assert.Equal("int", accountIdColumn.Detail);
        Assert.Contains("dbo.Accounts", accountIdColumn.Documentation);
    }

    #endregion

    #region Keyword Tests

    [Fact]
    public async Task AfterFrom_NoKeywords()
    {
        var context = new CompletionContext("SELECT * FROM ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should not have any keywords
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
        Assert.DoesNotContain(result, c => c.Label == "SELECT");
        Assert.DoesNotContain(result, c => c.Label == "WHERE");
    }

    [Fact]
    public async Task AfterJoin_NoKeywords()
    {
        var context = new CompletionContext("SELECT * FROM A JOIN ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterWhere_NoKeywords()
    {
        var context = new CompletionContext("SELECT * FROM A WHERE ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task AfterSelect_IncludesKeywords()
    {
        var context = new CompletionContext("SELECT ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should have SQL keywords
        Assert.Contains(result, c => c.Kind == "Keyword");
        Assert.Contains(result, c => c.Label == "FROM");
        Assert.Contains(result, c => c.Label == "WHERE");
        Assert.Contains(result, c => c.Label == "ORDER BY");
    }

    #endregion

    #region Case Insensitivity Tests

    [Fact]
    public async Task CaseInsensitive_FromLowercase()
    {
        var context = new CompletionContext("select * from ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should detect "from" regardless of case
        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task CaseInsensitive_JoinMixedCase()
    {
        var context = new CompletionContext("SELECT * FROM A InNeR jOiN ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.DoesNotContain(result, c => c.Kind == "Keyword");
    }

    [Fact]
    public async Task CaseInsensitive_WhereUppercase()
    {
        var context = new CompletionContext("SELECT * FROM A WHERE ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.Contains(result, c => c.Label.Contains("AccountId"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EmptyPrefix_ReturnsEmpty()
    {
        var context = new CompletionContext("", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.Empty(result);
    }

    [Fact]
    public async Task WhitespacePrefix_ReturnsEmpty()
    {
        var context = new CompletionContext("   ", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GeneralContext_ReturnsTablesAndKeywords()
    {
        var context = new CompletionContext("S", null, 0);
        var result = await _service.GetSchemaCompletionsAsync(context);

        // Should include both tables and keywords
        Assert.Contains(result, c => c.Label == "dbo.Accounts");
        Assert.Contains(result, c => c.Kind == "Keyword");
    }

    #endregion
}
