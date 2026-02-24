using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;

namespace SqlAuditedQueryTool.Llm.Services;

public class SchemaEmbeddingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchemaEmbeddingService> _logger;

    public SchemaEmbeddingService(IServiceProvider serviceProvider, ILogger<SchemaEmbeddingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for app to fully start before embedding
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var schemaProvider = scope.ServiceProvider.GetRequiredService<ISchemaProvider>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

            _logger.LogInformation("Starting schema embedding...");

            // Fetch schema metadata
            var schema = await schemaProvider.GetSchemaAsync(stoppingToken);
            
            if (schema == null)
            {
                _logger.LogWarning("Schema is null, skipping embedding");
                return;
            }

            var itemsToEmbed = new List<(string Key, string Text, VectorMetadata Metadata)>();

            // Prepare SQL keyword items
            AddSqlKeywords(itemsToEmbed);

            // Prepare table items
            foreach (var table in schema.Tables)
            {
                var tableKey = $"table:{table.SchemaName}.{table.TableName}";
                var tableText = $"{table.SchemaName}.{table.TableName} - table";
                var tableMetadata = new VectorMetadata
                {
                    Category = "schema",
                    DisplayText = $"{table.SchemaName}.{table.TableName}",
                    Description = $"Table: {table.SchemaName}.{table.TableName}",
                    Properties = new Dictionary<string, string>
                    {
                        { "kind", "Class" },
                        { "type", "table" },
                        { "schema", table.SchemaName },
                        { "table", table.TableName }
                    }
                };
                itemsToEmbed.Add((tableKey, tableText, tableMetadata));

                // Prepare column items for this table
                foreach (var column in table.Columns)
                {
                    var columnKey = $"column:{table.SchemaName}.{table.TableName}.{column.ColumnName}";
                    var columnText = $"{table.SchemaName}.{table.TableName}.{column.ColumnName} - {column.DataType} - column";
                    var columnMetadata = new VectorMetadata
                    {
                        Category = "schema",
                        DisplayText = column.ColumnName,
                        Description = $"{column.ColumnName} ({column.DataType}){(column.IsNullable ? " NULL" : " NOT NULL")}",
                        Properties = new Dictionary<string, string>
                        {
                            { "kind", "Field" },
                            { "type", column.DataType },
                            { "schema", table.SchemaName },
                            { "table", table.TableName },
                            { "column", column.ColumnName },
                            { "nullable", column.IsNullable.ToString() }
                        }
                    };
                    itemsToEmbed.Add((columnKey, columnText, columnMetadata));
                }
            }

            _logger.LogInformation("Embedding {Count} schema items...", itemsToEmbed.Count);

            // Batch embed all items
            var texts = itemsToEmbed.Select(x => x.Text).ToList();
            var embeddings = await embeddingService.EmbedBatchAsync(texts, stoppingToken);

            // Store embeddings
            for (int i = 0; i < itemsToEmbed.Count; i++)
            {
                var (key, _, metadata) = itemsToEmbed[i];
                await vectorStore.UpsertAsync(key, embeddings[i].ToArray(), metadata, stoppingToken);
            }

            _logger.LogInformation("Schema embedding completed: {Count} items embedded", itemsToEmbed.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during schema embedding");
        }
    }

    private static void AddSqlKeywords(List<(string Key, string Text, VectorMetadata Metadata)> itemsToEmbed)
    {
        // T-SQL keywords for autocomplete
        var keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN",
            "ON", "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "IS NULL", "IS NOT NULL",
            "ORDER BY", "GROUP BY", "HAVING", "DISTINCT", "TOP", "AS", "CASE", "WHEN", "THEN", "ELSE", "END",
            "INSERT", "UPDATE", "DELETE", "INTO", "VALUES", "SET",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "CAST", "CONVERT",
            "UNION", "UNION ALL", "EXCEPT", "INTERSECT",
            "WITH", "OVER", "PARTITION BY", "ROW_NUMBER", "RANK", "DENSE_RANK",
            "SUBSTRING", "TRIM", "UPPER", "LOWER", "CONCAT", "COALESCE", "ISNULL",
            "GETDATE", "DATEADD", "DATEDIFF", "YEAR", "MONTH", "DAY"
        };

        foreach (var keyword in keywords)
        {
            var keywordKey = $"keyword:{keyword}";
            var keywordText = $"{keyword} - SQL keyword";
            var keywordMetadata = new VectorMetadata
            {
                Category = "keyword",
                DisplayText = keyword,
                Description = $"T-SQL keyword: {keyword}",
                Properties = new Dictionary<string, string>
                {
                    { "kind", "Keyword" },
                    { "type", "keyword" }
                }
            };
            itemsToEmbed.Add((keywordKey, keywordText, keywordMetadata));
        }
    }
}
