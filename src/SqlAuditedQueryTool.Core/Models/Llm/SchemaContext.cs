namespace SqlAuditedQueryTool.Core.Models.Llm;

/// <summary>
/// Schema metadata only — table names, column names, and data types.
/// SAFETY: This type must NEVER contain actual database row data.
/// </summary>
public sealed class SchemaContext
{
    public required List<TableSchema> Tables { get; init; }
}

public sealed class TableSchema
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required List<ColumnSchema> Columns { get; init; }
    public List<string> PrimaryKey { get; init; } = [];
    public List<IndexSchema> Indexes { get; init; } = [];
    public List<ForeignKeySchema> ForeignKeys { get; init; } = [];
}

public sealed class ColumnSchema
{
    public required string ColumnName { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public int? MaxLength { get; init; }
    public bool IsPrimaryKey { get; init; }
    public bool IsIdentity { get; init; }
    public string? DefaultValue { get; init; }
    public bool IsComputed { get; init; }
}

public sealed class IndexSchema
{
    public required string Name { get; init; }
    public required List<string> Columns { get; init; }
    public bool IsUnique { get; init; }
    public bool IsClustered { get; init; }
}

public sealed class ForeignKeySchema
{
    public required string Name { get; init; }
    public required List<string> Columns { get; init; }
    public required string ReferencedTable { get; init; }
    public required string ReferencedSchema { get; init; }
    public required List<string> ReferencedColumns { get; init; }
}
