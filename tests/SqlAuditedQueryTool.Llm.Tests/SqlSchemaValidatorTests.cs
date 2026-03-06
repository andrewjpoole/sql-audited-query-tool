using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Services;

namespace SqlAuditedQueryTool.Llm.Tests;

public class SqlSchemaValidatorTests
{
    private static SchemaContext CreateTestSchema()
    {
        return new SchemaContext
        {
            Tables =
            [
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "Users",
                    Columns =
                    [
                        new ColumnSchema { ColumnName = "Id", DataType = "int", IsNullable = false },
                        new ColumnSchema { ColumnName = "Name", DataType = "nvarchar", IsNullable = false },
                        new ColumnSchema { ColumnName = "Email", DataType = "nvarchar", IsNullable = true },
                        new ColumnSchema { ColumnName = "CreatedAt", DataType = "datetime2", IsNullable = false }
                    ]
                },
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "Orders",
                    Columns =
                    [
                        new ColumnSchema { ColumnName = "Id", DataType = "int", IsNullable = false },
                        new ColumnSchema { ColumnName = "UserId", DataType = "int", IsNullable = false },
                        new ColumnSchema { ColumnName = "TotalAmount", DataType = "decimal", IsNullable = false },
                        new ColumnSchema { ColumnName = "OrderDate", DataType = "datetime2", IsNullable = false },
                        new ColumnSchema { ColumnName = "Status", DataType = "nvarchar", IsNullable = false }
                    ]
                },
                new TableSchema
                {
                    SchemaName = "dbo",
                    TableName = "Products",
                    Columns =
                    [
                        new ColumnSchema { ColumnName = "Id", DataType = "int", IsNullable = false },
                        new ColumnSchema { ColumnName = "ProductName", DataType = "nvarchar", IsNullable = false },
                        new ColumnSchema { ColumnName = "Price", DataType = "decimal", IsNullable = false }
                    ]
                }
            ]
        };
    }

    [Fact]
    public void Validate_ValidQuery_ReturnsNoWarnings()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT u.Name, u.Email FROM Users u WHERE u.Id = 1";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_InvalidTableName_ReturnsWarning()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT * FROM Cusomters";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Single(warnings);
        Assert.Contains("Cusomters", warnings[0]);
        Assert.Contains("not found", warnings[0]);
    }

    [Fact]
    public void Validate_MisspelledTableName_SuggestsCorrection()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT * FROM Ordrs";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Single(warnings);
        Assert.Contains("did you mean 'Orders'", warnings[0]);
    }

    [Fact]
    public void Validate_InvalidColumnName_ReturnsWarning()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT Users.Nme FROM Users";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Single(warnings);
        Assert.Contains("Nme", warnings[0]);
        Assert.Contains("did you mean 'Name'", warnings[0]);
    }

    [Fact]
    public void Validate_JoinWithInvalidTable_ReturnsWarning()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT u.Name FROM Users u JOIN Invoices i ON u.Id = i.UserId";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Contains(warnings, w => w.Contains("Invoices") && w.Contains("not found"));
    }

    [Fact]
    public void Validate_ValidJoinQuery_ReturnsNoWarnings()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT u.Name, o.TotalAmount FROM Users u JOIN Orders o ON u.Id = o.UserId";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_NullSchema_ReturnsEmpty()
    {
        var warnings = SqlSchemaValidator.Validate("SELECT 1", null!);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_EmptySql_ReturnsEmpty()
    {
        var schema = CreateTestSchema();
        var warnings = SqlSchemaValidator.Validate("", schema);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_SchemaQualifiedTable_ReturnsNoWarnings()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT * FROM [dbo].[Users]";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_AliasedColumnReference_ReturnsNoWarnings()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT o.TotalAmount, o.OrderDate FROM Orders o WHERE o.Status = 'Active'";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Empty(warnings);
    }

    [Fact]
    public void LevenshteinDistance_ExactMatch_ReturnsZero()
    {
        Assert.Equal(0, SqlSchemaValidator.LevenshteinDistance("hello", "hello"));
    }

    [Fact]
    public void LevenshteinDistance_SingleEdit_ReturnsOne()
    {
        Assert.Equal(1, SqlSchemaValidator.LevenshteinDistance("Name", "Nme"));
    }

    [Fact]
    public void LevenshteinDistance_EmptyStrings_ReturnsLength()
    {
        Assert.Equal(5, SqlSchemaValidator.LevenshteinDistance("", "hello"));
        Assert.Equal(5, SqlSchemaValidator.LevenshteinDistance("hello", ""));
    }

    [Fact]
    public void FindClosestMatch_ContainsMatch_ReturnsThat()
    {
        var result = SqlSchemaValidator.FindClosestMatch("User", ["Users", "Orders", "Products"]);
        Assert.Equal("Users", result);
    }

    [Fact]
    public void FindClosestMatch_NoCloseMatch_ReturnsNull()
    {
        var result = SqlSchemaValidator.FindClosestMatch("XYZ", ["Users", "Orders", "Products"]);
        Assert.Null(result);
    }

    [Fact]
    public void ExtractTableReferences_MultipleFromJoin_ExtractsAll()
    {
        var sql = "SELECT * FROM Users u JOIN Orders o ON u.Id = o.UserId LEFT JOIN Products p ON p.Id = 1";
        var refs = SqlSchemaValidator.ExtractTableReferences(sql);

        Assert.Equal(3, refs.Count);
        Assert.Contains(refs, r => r.TableName == "Users" && r.Alias == "u");
        Assert.Contains(refs, r => r.TableName == "Orders" && r.Alias == "o");
        Assert.Contains(refs, r => r.TableName == "Products" && r.Alias == "p");
    }

    [Fact]
    public void Validate_MisspelledColumnInAlias_SuggestsCorrection()
    {
        var schema = CreateTestSchema();
        var sql = "SELECT u.Emal FROM Users u";

        var warnings = SqlSchemaValidator.Validate(sql, schema);

        Assert.Single(warnings);
        Assert.Contains("Emal", warnings[0]);
        Assert.Contains("did you mean 'Email'", warnings[0]);
    }
}
