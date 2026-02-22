using System.Data.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Configuration;

namespace SqlAuditedQueryTool.Llm.Services;

/// <summary>
/// Queries INFORMATION_SCHEMA for table/column metadata.
/// SAFETY: Only reads schema metadata — NEVER queries or returns row data.
/// </summary>
public sealed class SchemaMetadataProvider : ISchemaProvider
{
    private const string CacheKey = "schema_metadata";

    private const string SchemaQuery = """
        SELECT 
            t.TABLE_SCHEMA,
            t.TABLE_NAME,
            c.COLUMN_NAME,
            c.DATA_TYPE,
            c.IS_NULLABLE,
            c.CHARACTER_MAXIMUM_LENGTH
        FROM INFORMATION_SCHEMA.TABLES t
        INNER JOIN INFORMATION_SCHEMA.COLUMNS c 
            ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
        WHERE t.TABLE_TYPE = 'BASE TABLE'
        ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME, c.ORDINAL_POSITION
        """;

    private const string ColumnExtrasQuery = """
        SELECT 
            s.name AS SchemaName,
            o.name AS TableName,
            c.name AS ColumnName,
            c.is_identity,
            c.is_computed,
            dc.definition AS DefaultValue
        FROM sys.columns c
        INNER JOIN sys.objects o ON c.object_id = o.object_id
        INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
        WHERE o.type = 'U'
        ORDER BY s.name, o.name, c.column_id
        """;

    private const string PrimaryKeyQuery = """
        SELECT 
            s.name AS SchemaName,
            t.name AS TableName,
            c.name AS ColumnName
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE i.is_primary_key = 1
        ORDER BY s.name, t.name, ic.key_ordinal
        """;

    private const string IndexQuery = """
        SELECT 
            s.name AS SchemaName,
            t.name AS TableName,
            i.name AS IndexName,
            c.name AS ColumnName,
            i.is_unique,
            i.type_desc,
            ic.key_ordinal
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        INNER JOIN sys.tables t ON i.object_id = t.object_id
        INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
        WHERE i.is_primary_key = 0 AND i.type > 0 AND i.name IS NOT NULL
        ORDER BY s.name, t.name, i.name, ic.key_ordinal
        """;

    private const string ForeignKeyQuery = """
        SELECT 
            ps.name AS SchemaName,
            pt.name AS TableName,
            fk.name AS FkName,
            pc.name AS ColumnName,
            rs.name AS ReferencedSchema,
            rt.name AS ReferencedTable,
            rc.name AS ReferencedColumn
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
        INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
        INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
        INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
        INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
        INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id
        """;

    private readonly IConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;
    private readonly OllamaOptions _options;
    private readonly ILogger<SchemaMetadataProvider> _logger;

    public SchemaMetadataProvider(
        IConnectionFactory connectionFactory,
        IMemoryCache cache,
        IOptions<OllamaOptions> options,
        ILogger<SchemaMetadataProvider> logger)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SchemaContext> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out SchemaContext? cached) && cached is not null)
        {
            _logger.LogDebug("Returning cached schema metadata");
            return cached;
        }

        _logger.LogInformation("Loading schema metadata from database");
        var schema = await LoadSchemaAsync(cancellationToken);

        _cache.Set(CacheKey, schema, _options.SchemaCacheDuration);
        return schema;
    }

    private async Task<SchemaContext> LoadSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        // Load base column info from INFORMATION_SCHEMA
        var tables = await LoadBaseSchemaAsync(connection, cancellationToken);

        // Enrich with sys.* catalog metadata
        await LoadColumnExtrasAsync(connection, tables, cancellationToken);
        await LoadPrimaryKeysAsync(connection, tables, cancellationToken);
        await LoadIndexesAsync(connection, tables, cancellationToken);
        await LoadForeignKeysAsync(connection, tables, cancellationToken);

        var schema = new SchemaContext { Tables = tables.Values.ToList() };
        _logger.LogInformation("Loaded schema metadata: {TableCount} tables", schema.Tables.Count);
        return schema;
    }

    private async Task<Dictionary<string, TableSchema>> LoadBaseSchemaAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SchemaQuery;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new Dictionary<string, TableSchema>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(0);
            var tableName = reader.GetString(1);
            var key = $"{schemaName}.{tableName}";

            if (!tables.TryGetValue(key, out var table))
            {
                table = new TableSchema
                {
                    SchemaName = schemaName,
                    TableName = tableName,
                    Columns = []
                };
                tables[key] = table;
            }

            var maxLength = reader.IsDBNull(5) ? (int?)null : Convert.ToInt32(reader.GetValue(5));

            table.Columns.Add(new ColumnSchema
            {
                ColumnName = reader.GetString(2),
                DataType = reader.GetString(3),
                IsNullable = reader.GetString(4) == "YES",
                MaxLength = maxLength
            });
        }

        return tables;
    }

    private async Task LoadColumnExtrasAsync(
        DbConnection connection, Dictionary<string, TableSchema> tables, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ColumnExtrasQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tables.TryGetValue(key, out var table)) continue;

            var columnName = reader.GetString(2);
            var column = table.Columns.Find(c => c.ColumnName == columnName);
            if (column is null) continue;

            var isIdentity = reader.GetBoolean(3);
            var isComputed = reader.GetBoolean(4);
            var defaultValue = reader.IsDBNull(5) ? null : reader.GetString(5);

            if (!isIdentity && !isComputed && defaultValue is null) continue;

            // Replace column with enriched version (records are immutable via init)
            var idx = table.Columns.IndexOf(column);
            table.Columns[idx] = new ColumnSchema
            {
                ColumnName = column.ColumnName,
                DataType = column.DataType,
                IsNullable = column.IsNullable,
                MaxLength = column.MaxLength,
                IsIdentity = isIdentity,
                IsComputed = isComputed,
                DefaultValue = defaultValue
            };
        }
    }

    private async Task LoadPrimaryKeysAsync(
        DbConnection connection, Dictionary<string, TableSchema> tables, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = PrimaryKeyQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var key = $"{reader.GetString(0)}.{reader.GetString(1)}";
            if (!tables.TryGetValue(key, out var table)) continue;

            var columnName = reader.GetString(2);
            table.PrimaryKey.Add(columnName);

            // Mark the column as primary key
            var column = table.Columns.Find(c => c.ColumnName == columnName);
            if (column is null) continue;

            var idx = table.Columns.IndexOf(column);
            table.Columns[idx] = new ColumnSchema
            {
                ColumnName = column.ColumnName,
                DataType = column.DataType,
                IsNullable = column.IsNullable,
                MaxLength = column.MaxLength,
                IsPrimaryKey = true,
                IsIdentity = column.IsIdentity,
                IsComputed = column.IsComputed,
                DefaultValue = column.DefaultValue
            };
        }
    }

    private async Task LoadIndexesAsync(
        DbConnection connection, Dictionary<string, TableSchema> tables, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = IndexQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        // Group index columns by table+index name
        var indexMap = new Dictionary<string, (string TableKey, string IndexName, bool IsUnique, string TypeDesc, List<string> Columns)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var tableKey = $"{reader.GetString(0)}.{reader.GetString(1)}";
            var indexName = reader.GetString(2);
            var columnName = reader.GetString(3);
            var isUnique = reader.GetBoolean(4);
            var typeDesc = reader.GetString(5);

            var indexKey = $"{tableKey}.{indexName}";
            if (!indexMap.TryGetValue(indexKey, out var entry))
            {
                entry = (tableKey, indexName, isUnique, typeDesc, new List<string>());
                indexMap[indexKey] = entry;
            }
            entry.Columns.Add(columnName);
        }

        foreach (var entry in indexMap.Values)
        {
            if (!tables.TryGetValue(entry.TableKey, out var table)) continue;

            table.Indexes.Add(new IndexSchema
            {
                Name = entry.IndexName,
                Columns = entry.Columns,
                IsUnique = entry.IsUnique,
                IsClustered = entry.TypeDesc.Equals("CLUSTERED", StringComparison.OrdinalIgnoreCase)
            });
        }
    }

    private async Task LoadForeignKeysAsync(
        DbConnection connection, Dictionary<string, TableSchema> tables, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ForeignKeyQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var fkMap = new Dictionary<string, (string TableKey, string FkName, string RefSchema, string RefTable, List<string> Columns, List<string> RefColumns)>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var tableKey = $"{reader.GetString(0)}.{reader.GetString(1)}";
            var fkName = reader.GetString(2);
            var columnName = reader.GetString(3);
            var refSchema = reader.GetString(4);
            var refTable = reader.GetString(5);
            var refColumn = reader.GetString(6);

            var fkKey = $"{tableKey}.{fkName}";
            if (!fkMap.TryGetValue(fkKey, out var entry))
            {
                entry = (tableKey, fkName, refSchema, refTable, new List<string>(), new List<string>());
                fkMap[fkKey] = entry;
            }
            entry.Columns.Add(columnName);
            entry.RefColumns.Add(refColumn);
        }

        foreach (var entry in fkMap.Values)
        {
            if (!tables.TryGetValue(entry.TableKey, out var table)) continue;

            table.ForeignKeys.Add(new ForeignKeySchema
            {
                Name = entry.FkName,
                Columns = entry.Columns,
                ReferencedSchema = entry.RefSchema,
                ReferencedTable = entry.RefTable,
                ReferencedColumns = entry.RefColumns
            });
        }
    }
}
