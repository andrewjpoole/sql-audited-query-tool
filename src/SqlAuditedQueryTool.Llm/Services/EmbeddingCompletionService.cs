using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using System.Text.RegularExpressions;

namespace SqlAuditedQueryTool.Llm.Services;

public class EmbeddingCompletionService : ICompletionService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;

    public EmbeddingCompletionService(IEmbeddingService embeddingService, IVectorStore vectorStore)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task<IReadOnlyList<CompletionItem>> GetSchemaCompletionsAsync(
        CompletionContext context, 
        CancellationToken ct = default)
    {
        // Use prefix for context detection
        if (string.IsNullOrWhiteSpace(context.Prefix))
            return Array.Empty<CompletionItem>();

        // Detect SQL context to filter results appropriately
        var sqlContext = DetectSqlContext(context.Prefix);

        // Extract the word at cursor for embedding (more accurate semantic search)
        var wordAtCursor = ExtractWordAtCursor(context.Prefix);
        var searchText = string.IsNullOrWhiteSpace(wordAtCursor) ? context.Prefix : wordAtCursor;

        // Embed the search text (just the word at cursor, not the full query)
        var queryEmbedding = await _embeddingService.EmbedAsync(searchText, ct);

        // Search vector store for similar schema items and keywords
        var schemaResults = await _vectorStore.SearchAsync(queryEmbedding, topK: 30, category: "schema", ct);
        var keywordResults = await _vectorStore.SearchAsync(queryEmbedding, topK: 20, category: "keyword", ct);
        
        // Combine results
        var allResults = schemaResults.Concat(keywordResults).OrderByDescending(r => r.Score).ToList();

        // Filter results based on SQL context
        var filteredResults = FilterByContext(allResults, sqlContext);

        // Extract prefix for boosting
        var prefix = context.Prefix.Trim();

        // Convert to completion items with prefix boosting
        var completions = filteredResults.Select(r =>
        {
            var baseScore = r.Score;
            
            // Extract the actual word from prefix for matching (e.g., "u" from "SELECT * FROM u")
            var wordAtCursor = ExtractWordAtCursor(prefix);
            
            // Apply strong prefix boosting to prioritize exact matches
            if (!string.IsNullOrEmpty(wordAtCursor))
            {
                var displayText = r.Metadata.DisplayText;
                var isKeyword = r.Metadata.Category == "keyword";
                
                // For schema items, also check against specific property names (table/column name without schema)
                string? tableName = r.Metadata.Properties.GetValueOrDefault("table");
                string? columnName = r.Metadata.Properties.GetValueOrDefault("column");
                
                // Check exact match on display text or property names
                if (displayText.Equals(wordAtCursor, StringComparison.OrdinalIgnoreCase) ||
                    (tableName != null && tableName.Equals(wordAtCursor, StringComparison.OrdinalIgnoreCase)) ||
                    (columnName != null && columnName.Equals(wordAtCursor, StringComparison.OrdinalIgnoreCase)))
                {
                    // Exact match - highest boost
                    baseScore += 1000.0f;
                }
                else if (displayText.StartsWith(wordAtCursor, StringComparison.OrdinalIgnoreCase) ||
                         (tableName != null && tableName.StartsWith(wordAtCursor, StringComparison.OrdinalIgnoreCase)) ||
                         (columnName != null && columnName.StartsWith(wordAtCursor, StringComparison.OrdinalIgnoreCase)))
                {
                    // Prefix match - boost increases with match length (progressive matching)
                    // More characters matched = higher boost
                    var matchLength = wordAtCursor.Length;
                    var lengthRatio = (float)matchLength / displayText.Length;
                    
                    // Base prefix boost, scaled by how much of the word is matched
                    // Keywords get extra boost since they're more likely what user wants
                    var basePrefixBoost = isKeyword ? 500.0f : 100.0f;
                    var progressiveBoost = basePrefixBoost * (1.0f + lengthRatio * 2.0f);
                    
                    baseScore += progressiveBoost;
                }
                else if (displayText.Contains(wordAtCursor, StringComparison.OrdinalIgnoreCase))
                {
                    // Substring match on display text only - smaller boost
                    baseScore += 10.0f;
                }
            }
            
            return new CompletionItem
            {
                Label = r.Metadata.DisplayText,
                InsertText = r.Metadata.DisplayText,
                Kind = DetermineCompletionKind(r.Metadata),
                Detail = r.Metadata.Properties.GetValueOrDefault("type"),
                Documentation = r.Metadata.Description,
                Score = baseScore
            };
        })
        .OrderByDescending(c => c.Score)
        .ToList();

        return completions;
    }

    private static SqlContextType DetectSqlContext(string text)
    {
        // Normalize text for pattern matching (don't trim - we need trailing spaces)
        var normalizedText = text.ToUpperInvariant();

        // Check for table reference with dot (e.g., "Users.", "dbo.Users.")
        if (Regex.IsMatch(normalizedText, @"\w+\.\s*$"))
        {
            return SqlContextType.AfterTableDot;
        }

        // Check for FROM keyword (want table names)
        if (Regex.IsMatch(normalizedText, @"\bFROM\s+\w*$", RegexOptions.IgnoreCase))
        {
            return SqlContextType.AfterFrom;
        }

        // Check for JOIN keyword (want table names)
        if (Regex.IsMatch(normalizedText, @"\b(INNER\s+JOIN|LEFT\s+JOIN|RIGHT\s+JOIN|FULL\s+JOIN|JOIN)\s+\w*$", RegexOptions.IgnoreCase))
        {
            return SqlContextType.AfterJoin;
        }

        // Check for SELECT keyword (want columns)
        if (Regex.IsMatch(normalizedText, @"\bSELECT\s+\w*$", RegexOptions.IgnoreCase))
        {
            return SqlContextType.AfterSelect;
        }

        // Check for WHERE, AND, OR (want columns for filtering)
        if (Regex.IsMatch(normalizedText, @"\b(WHERE|AND|OR)\s+\w*$", RegexOptions.IgnoreCase))
        {
            return SqlContextType.AfterWhere;
        }

        // Default: could be anything
        return SqlContextType.General;
    }

    private static IReadOnlyList<VectorSearchResult> FilterByContext(
        IReadOnlyList<VectorSearchResult> results, 
        SqlContextType context)
    {
        // Keywords should always be shown (except in very specific contexts)
        var keywords = results.Where(r => r.Metadata.Category == "keyword").ToList();
        var schemaItems = results.Where(r => r.Metadata.Category == "schema").ToList();

        IEnumerable<VectorSearchResult> filteredSchema = context switch
        {
            // After FROM/JOIN -> only show tables
            SqlContextType.AfterFrom or SqlContextType.AfterJoin =>
                schemaItems.Where(r => r.Metadata.Properties.ContainsKey("table") && 
                                     !r.Metadata.Properties.ContainsKey("column")),

            // After table.dot -> only show columns from that table
            SqlContextType.AfterTableDot =>
                schemaItems.Where(r => r.Metadata.Properties.ContainsKey("column")),

            // After SELECT/WHERE -> prefer columns, but allow tables
            SqlContextType.AfterSelect or SqlContextType.AfterWhere =>
                schemaItems.OrderByDescending(r => r.Metadata.Properties.ContainsKey("column") ? 1 : 0)
                           .ThenByDescending(r => r.Score),

            // General context -> show everything
            _ => schemaItems
        };

        // Combine keywords with filtered schema items, preserving their individual scores
        return keywords.Concat(filteredSchema).ToList();
    }

    private static string DetermineCompletionKind(VectorMetadata metadata)
    {
        // Determine Monaco completion kind based on metadata properties
        if (metadata.Properties.TryGetValue("kind", out var kind))
            return kind;

        // Default fallback based on category
        return metadata.Category switch
        {
            "schema" => metadata.Properties.ContainsKey("table") ? "Field" : "Class",
            _ => "Text"
        };
    }

    private static string ExtractWordAtCursor(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        // Get the last word (alphanumeric or dot) from the prefix
        var match = Regex.Match(prefix, @"[a-zA-Z0-9_.]+$");
        return match.Success ? match.Value : prefix;
    }

    private enum SqlContextType
    {
        General,
        AfterSelect,
        AfterFrom,
        AfterJoin,
        AfterWhere,
        AfterTableDot
    }
}
