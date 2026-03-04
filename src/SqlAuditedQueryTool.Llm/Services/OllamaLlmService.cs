using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Llm.Configuration;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AIChatRole = Microsoft.Extensions.AI.ChatRole;
using OllamaChatRole = OllamaSharp.Models.Chat.ChatRole;

namespace SqlAuditedQueryTool.Llm.Services;

public sealed class OllamaLlmService : ILlmService
{
    private static readonly Regex ThinkingContentRegex = new(
        @"<think>.*?</think>\s*",
        RegexOptions.Singleline | RegexOptions.Compiled);

    internal const string DefaultSystemPrompt =
        "You are a SQL Server query assistant for incident investigation. " +
        "You help investigate incidents by executing queries and analyzing results. " +
        "Use the execute_sql_query tool to run SELECT queries when needed. " +
        "After seeing results, provide analysis and suggest follow-up queries if helpful. " +
        "\n\nYou also have access to code context tools to read and analyze application code repositories. " +
        "Use ReadFile, ListFiles, SearchCode, and AnalyzeCode to understand database structure and patterns from code (EF Core, Dapper, ADO.NET). " +
        "Use AddContextDirectory to add new directories to the allowed list for this session.";

    private readonly IChatClient _client;
    private readonly IOllamaApiClient _ollamaClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaLlmService> _logger;
    private readonly IQueryExecutor? _queryExecutor;
    private readonly ICodeContextService? _codeContextService;

    public OllamaLlmService(
        IChatClient client,
        IOllamaApiClient ollamaClient,
        IOptions<OllamaOptions> options, 
        ILogger<OllamaLlmService> logger,
        IQueryExecutor? queryExecutor = null,
        ICodeContextService? codeContextService = null)
    {
        _client = client;
        _ollamaClient = ollamaClient;
        _options = options.Value;
        _logger = logger;
        _queryExecutor = queryExecutor;
        _codeContextService = codeContextService;
    }

    public async Task<LlmResponse> ChatAsync(LlmChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending chat request to Ollama model {Model}", _options.Model);

        // Build Ollama native request using OllamaSharp types
        var chatRequest = new ChatRequest
        {
            Model = _options.Model,
            Messages = BuildOllamaMessages(request),
            Tools = BuildOllamaTools(),
            Stream = false  // Non-streaming mode to get full response with tool calls
        };

        // Use IOllamaApiClient directly - ChatAsync returns IAsyncEnumerable even with Stream=false
        // We need to collect all chunks to get the final response
        ChatResponseStream? finalResponse = null;
        await foreach (var chunk in _ollamaClient.ChatAsync(chatRequest, cancellationToken))
        {
            if (chunk != null)
            {
                finalResponse = chunk;
            }
        }
        
        _logger.LogDebug("Received response from Ollama");
        
        // Extract tool calls from the final response
        var toolCalls = finalResponse?.Message != null 
            ? ExtractToolCallsFromOllama(finalResponse.Message) 
            : new List<ToolCallRequest>();
        
        var rawText = finalResponse?.Message?.Content ?? string.Empty;
        var text = StripThinkingContent(rawText);

        return new LlmResponse
        {
            Text = text,
            SuggestedQueries = ParseSuggestedQueries(text),
            ToolCalls = toolCalls
        };
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        LlmChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(request);

        var chatOptions = new ChatOptions
        {
            ModelId = _options.Model,
            Tools = BuildTools()
        };

        var filter = new StreamingThinkingFilter();
        await foreach (var update in _client.GetStreamingResponseAsync(messages, chatOptions, cancellationToken: cancellationToken))
        {
            if (update.Text is { Length: > 0 } content)
            {
                foreach (var filtered in filter.ProcessChunk(content))
                {
                    yield return filtered;
                }
            }
        }
    }

    private static List<AIChatMessage> BuildMessages(LlmChatRequest request)
    {
        var messages = new List<AIChatMessage>();

        var systemPrompt = request.SystemPrompt ?? DefaultSystemPrompt;
        if (request.SchemaContext is { Tables.Count: > 0 } schema)
        {
            systemPrompt += "\n\nAvailable database schema (metadata only — no row data):\n" + FormatSchema(schema);
        }
        messages.Add(new AIChatMessage(AIChatRole.System, systemPrompt));

        foreach (var msg in request.Messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "system" => AIChatRole.System,
                "assistant" => AIChatRole.Assistant,
                _ => AIChatRole.User
            };
            messages.Add(new AIChatMessage(role, msg.Content));
        }

        return messages;
    }

    private static Message[] BuildOllamaMessages(LlmChatRequest request)
    {
        var messages = new List<Message>();

        var systemPrompt = request.SystemPrompt ?? DefaultSystemPrompt;
        if (request.SchemaContext is { Tables.Count: > 0 } schema)
        {
            systemPrompt += "\n\nAvailable database schema (metadata only — no row data):\n" + FormatSchema(schema);
        }
        messages.Add(new Message
        {
            Role = OllamaChatRole.System,
            Content = systemPrompt
        });

        foreach (var msg in request.Messages)
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "system" => OllamaChatRole.System,
                "assistant" => OllamaChatRole.Assistant,
                _ => OllamaChatRole.User
            };
            messages.Add(new Message
            {
                Role = role,
                Content = msg.Content
            });
        }

        return messages.ToArray();
    }

    private static string FormatSchema(SchemaContext schema)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var table in schema.Tables)
        {
            sb.AppendLine($"[{table.SchemaName}].[{table.TableName}]");
            foreach (var col in table.Columns)
            {
                var nullable = col.IsNullable ? "NULL" : "NOT NULL";
                var maxLen = col.MaxLength.HasValue ? $"({col.MaxLength})" : "";
                sb.AppendLine($"  - {col.ColumnName} {col.DataType}{maxLen} {nullable}");
            }
        }
        return sb.ToString();
    }

    internal static List<SuggestedQuery> ParseSuggestedQueries(string text)
    {
        var queries = new List<SuggestedQuery>();
        var codeBlockPattern = new System.Text.RegularExpressions.Regex(
            @"```sql\s*\n(.*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in codeBlockPattern.Matches(text))
        {
            var sql = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(sql)) continue;

            var isFixQuery = IsFixQuery(sql, text, match.Index);
            queries.Add(new SuggestedQuery
            {
                Sql = sql,
                IsReadOnly = !isFixQuery,
                IsFixQuery = isFixQuery
            });
        }

        return queries;
    }

    private static bool IsFixQuery(string sql, string fullText, int matchIndex)
    {
        var sqlUpper = sql.TrimStart().ToUpperInvariant();
        if (sqlUpper.StartsWith("INSERT") || sqlUpper.StartsWith("UPDATE") ||
            sqlUpper.StartsWith("DELETE") || sqlUpper.StartsWith("ALTER") ||
            sqlUpper.StartsWith("DROP") || sqlUpper.StartsWith("CREATE") ||
            sqlUpper.StartsWith("EXEC") || sqlUpper.StartsWith("MERGE"))
        {
            return true;
        }

        // Check if preceded by "FIX QUERY" label
        var preceding = fullText[..matchIndex];
        return preceding.Contains("FIX QUERY", StringComparison.OrdinalIgnoreCase);
    }

    private List<AITool> BuildTools()
    {
        var tools = new List<AITool>();

        // Add query execution tool (placeholder for when tool calling is supported)
        // Currently Ollama tool calling may not be fully supported
        
        // Add code context tools if service is available
        if (_codeContextService != null)
        {
            tools.Add(AIFunctionFactory.Create(ReadFileAsync, "ReadFile"));
            tools.Add(AIFunctionFactory.Create(ListFilesAsync, "ListFiles"));
            tools.Add(AIFunctionFactory.Create(SearchCodeAsync, "SearchCode"));
            tools.Add(AIFunctionFactory.Create(AnalyzeCodeAsync, "AnalyzeCode"));
            tools.Add(AIFunctionFactory.Create(AddContextDirectoryAsync, "AddContextDirectory"));
            tools.Add(AIFunctionFactory.Create(RemoveContextDirectoryAsync, "RemoveContextDirectory"));
            tools.Add(AIFunctionFactory.Create(ListContextDirectoriesAsync, "ListContextDirectories"));
        }

        return tools;
    }

    private IEnumerable<object>? BuildOllamaTools()
    {
        if (_codeContextService == null) return null;

        var tools = new List<object>
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "ReadFile",
                    description = "Read the content of a specific file",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["path"] = new
                            {
                                type = "string",
                                description = "The path to the file to read"
                            }
                        },
                        required = new[] { "path" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "ListFiles",
                    description = "List files in a directory matching a pattern",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["directory"] = new
                            {
                                type = "string",
                                description = "The directory to search in"
                            },
                            ["pattern"] = new
                            {
                                type = "string",
                                description = "The file pattern to match (e.g., *.cs, *DbContext.cs)"
                            }
                        },
                        required = new[] { "directory" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "SearchCode",
                    description = "Search for code patterns across files",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["searchPattern"] = new
                            {
                                type = "string",
                                description = "The regex pattern to search for"
                            },
                            ["directory"] = new
                            {
                                type = "string",
                                description = "The directory to search in"
                            }
                        },
                        required = new[] { "searchPattern" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "AnalyzeCode",
                    description = "Analyze application code in a directory. Extracts class definitions, methods, properties, and specially detects database-related patterns including Entity Framework DbContext classes, Dapper queries, and ADO.NET usage.",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["directory"] = new
                            {
                                type = "string",
                                description = "The directory to analyze for code patterns"
                            }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "AddContextDirectory",
                    description = "Add a directory to the allowed list for this chat session",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["directory"] = new
                            {
                                type = "string",
                                description = "The full path to the directory to allow"
                            }
                        },
                        required = new[] { "directory" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "RemoveContextDirectory",
                    description = "Remove a directory from the session allowed list",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["directory"] = new
                            {
                                type = "string",
                                description = "The directory path to remove"
                            }
                        },
                        required = new[] { "directory" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "ListContextDirectories",
                    description = "List all directories currently allowed for code context access",
                    parameters = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>(),
                        required = Array.Empty<string>()
                    }
                }
            }
        };

        return tools;
    }

    [System.ComponentModel.Description("Read the content of a specific file")]
    private async Task<string> ReadFileAsync(
        [System.ComponentModel.Description("The path to the file to read")] string path,
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return JsonSerializer.Serialize(new { success = false, error = "Code context service not available" });
        
        try
        {
            _logger.LogInformation("LLM requested to read file: {Path}", path);
            var result = await _codeContextService.ReadFileAsync(path, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                path = result.Path,
                sizeBytes = result.SizeBytes,
                lastModified = result.LastModified,
                content = result.Content
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {Path}", path);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [System.ComponentModel.Description("List files in a directory matching a pattern")]
    private async Task<string> ListFilesAsync(
        [System.ComponentModel.Description("The directory to search in")] string directory,
        [System.ComponentModel.Description("The file pattern to match (e.g., *.cs, *DbContext.cs)")] string pattern = "*.cs",
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return JsonSerializer.Serialize(new { success = false, error = "Code context service not available" });
        
        try
        {
            _logger.LogInformation("LLM requested to list files in {Directory} with pattern {Pattern}", directory, pattern);
            var result = await _codeContextService.ListFilesAsync(directory, pattern, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                directory = result.Directory,
                totalCount = result.TotalCount,
                truncated = result.Truncated,
                files = result.Files.Select(f => new
                {
                    path = f.Path,
                    name = f.Name,
                    sizeBytes = f.SizeBytes,
                    lastModified = f.LastModified
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files in {Directory}", directory);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [System.ComponentModel.Description("Search for code patterns across files")]
    private async Task<string> SearchCodeAsync(
        [System.ComponentModel.Description("The regex pattern to search for")] string searchPattern,
        [System.ComponentModel.Description("The directory to search in")] string directory = ".",
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return JsonSerializer.Serialize(new { success = false, error = "Code context service not available" });
        
        try
        {
            _logger.LogInformation("LLM requested code search for pattern: {Pattern} in {Directory}", searchPattern, directory);
            var result = await _codeContextService.SearchCodeAsync(searchPattern, directory, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                searchPattern = result.SearchPattern,
                totalCount = result.TotalCount,
                truncated = result.Truncated,
                matches = result.Matches.Select(m => new
                {
                    path = m.FilePath,
                    line = m.LineNumber,
                    text = m.LineContent
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching code in {Directory}", directory);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [System.ComponentModel.Description("Analyze application code in a directory. Extracts class definitions, methods, properties, and specially detects database-related patterns including Entity Framework DbContext classes, Dapper queries, and ADO.NET usage.")]
    private async Task<string> AnalyzeCodeAsync(
        [System.ComponentModel.Description("The directory to analyze (default: current directory)")] string directory = ".",
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return JsonSerializer.Serialize(new { success = false, error = "Code context service not available" });
        
        try
        {
            _logger.LogInformation("LLM requested code analysis in directory: {Directory}", directory);
            var result = await _codeContextService.AnalyzeCodeAsync(directory, cancellationToken);
            
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing code in: {Directory}", directory);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [System.ComponentModel.Description("Add a directory to the allowed list for this chat session")]
    private Task<string> AddContextDirectoryAsync(
        [System.ComponentModel.Description("The full path to the directory to allow")] string directory,
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "Code context service not available" }));
        
        try
        {
            _logger.LogInformation("LLM requested to add context directory: {Directory}", directory);
            _codeContextService.AddAllowedDirectory(directory);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Directory added to allowed list: {directory}",
                allowedDirectories = _codeContextService.GetAllowedDirectories()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding context directory: {Directory}", directory);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    [System.ComponentModel.Description("Remove a directory from the session allowed list")]
    private Task<string> RemoveContextDirectoryAsync(
        [System.ComponentModel.Description("The directory path to remove")] string directory,
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "Code context service not available" }));
        
        try
        {
            _logger.LogInformation("LLM requested to remove context directory: {Directory}", directory);
            var removed = _codeContextService.RemoveAllowedDirectory(directory);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = removed,
                message = removed ? $"Directory removed: {directory}" : $"Directory not found in session list: {directory}",
                allowedDirectories = _codeContextService.GetAllowedDirectories()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing context directory: {Directory}", directory);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    [System.ComponentModel.Description("List all directories currently allowed for code context access")]
    private Task<string> ListContextDirectoriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_codeContextService == null) return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "Code context service not available" }));
        
        try
        {
            _logger.LogInformation("LLM requested list of context directories");
            var directories = _codeContextService.GetAllowedDirectories();
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                totalCount = directories.Count,
                directories = directories,
                note = "Includes both config-based and session-added directories"
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing context directories");
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    // TODO: Enable when Ollama tool calling is properly supported
    /*
    private static List<AITool> BuildTools()
    {
        return
        [
            AIFunctionFactory.Create(
                (string sql, string reason) => new { sql, reason },
                name: "execute_sql_query",
                description: "Execute a readonly SQL SELECT query against the database and return results for analysis")
        ];
    }
    */

    private List<ToolCallRequest> ExtractToolCallsFromOllama(Message message)
    {
        var toolCalls = new List<ToolCallRequest>();
        
        try
        {
            // OllamaSharp Message has ToolCalls property
            if (message?.ToolCalls != null)
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    if (toolCall?.Function == null) continue;

                    var arguments = new Dictionary<string, object?>();
                    
                    // Parse the arguments from the function call
                    if (toolCall.Function.Arguments != null)
                    {
                        try
                        {
                            // Arguments is an object - try to convert to dictionary
                            var json = JsonSerializer.Serialize(toolCall.Function.Arguments);
                            var deserializedArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                            if (deserializedArgs != null)
                            {
                                foreach (var kvp in deserializedArgs)
                                {
                                    // Convert JsonElement to appropriate type
                                    arguments[kvp.Key] = kvp.Value.ValueKind switch
                                    {
                                        JsonValueKind.String => kvp.Value.GetString(),
                                        JsonValueKind.Number => kvp.Value.GetDouble(),
                                        JsonValueKind.True or JsonValueKind.False => kvp.Value.GetBoolean(),
                                        JsonValueKind.Null => null,
                                        _ => kvp.Value.ToString()
                                    };
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse tool call arguments for {ToolName}", toolCall.Function.Name);
                        }
                    }
                    
                    toolCalls.Add(new ToolCallRequest
                    {
                        ToolCallId = Guid.NewGuid().ToString(),
                        ToolName = toolCall.Function.Name ?? "UnknownTool",
                        Arguments = arguments
                    });
                    
                    _logger.LogInformation("Extracted tool call: {ToolName}", toolCall.Function.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tool calls from Ollama message");
        }
        
        _logger.LogInformation("Total tool calls extracted: {Count}", toolCalls.Count);
        return toolCalls;
    }

    public async Task<string> ExecuteToolCallAsync(ToolCallRequest toolCall, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing tool call: {ToolName}", toolCall.ToolName);

        try
        {
            switch (toolCall.ToolName)
            {
                case "execute_sql_query":
                    return await ExecuteSqlQueryToolAsync(toolCall.Arguments, cancellationToken);
                
                case "ReadFile":
                    if (toolCall.Arguments.TryGetValue("path", out var pathObj) && pathObj is string path)
                        return await ReadFileAsync(path, cancellationToken);
                    return JsonSerializer.Serialize(new { success = false, error = "Missing 'path' argument" });
                
                case "ListFiles":
                    var dir = toolCall.Arguments.TryGetValue("directory", out var dirObj) && dirObj is string d ? d : ".";
                    var pattern = toolCall.Arguments.TryGetValue("pattern", out var patObj) && patObj is string p ? p : "*.cs";
                    return await ListFilesAsync(dir, pattern, cancellationToken);
                
                case "SearchCode":
                    if (!toolCall.Arguments.TryGetValue("searchPattern", out var searchObj) || searchObj is not string searchPattern)
                        return JsonSerializer.Serialize(new { success = false, error = "Missing 'searchPattern' argument" });
                    var searchDir = toolCall.Arguments.TryGetValue("directory", out var sdObj) && sdObj is string sd ? sd : ".";
                    return await SearchCodeAsync(searchPattern, searchDir, cancellationToken);
                
                case "AnalyzeCode":
                    var analysisDir = toolCall.Arguments.TryGetValue("directory", out var adObj) && adObj is string ad ? ad : ".";
                    return await AnalyzeCodeAsync(analysisDir, cancellationToken);
                
                case "AddContextDirectory":
                    if (!toolCall.Arguments.TryGetValue("directory", out var addDirObj) || addDirObj is not string addDir)
                        return JsonSerializer.Serialize(new { success = false, error = "Missing 'directory' argument" });
                    return await AddContextDirectoryAsync(addDir, cancellationToken);
                
                case "RemoveContextDirectory":
                    if (!toolCall.Arguments.TryGetValue("directory", out var remDirObj) || remDirObj is not string remDir)
                        return JsonSerializer.Serialize(new { success = false, error = "Missing 'directory' argument" });
                    return await RemoveContextDirectoryAsync(remDir, cancellationToken);
                
                case "ListContextDirectories":
                    return await ListContextDirectoriesAsync(cancellationToken);
                
                default:
                    return JsonSerializer.Serialize(new { success = false, error = $"Unknown tool '{toolCall.ToolName}'" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.ToolName);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task<string> ExecuteSqlQueryToolAsync(Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        if (!args.TryGetValue("sql", out var sqlObj) || sqlObj is not string sql)
        {
            return "Error: Missing or invalid 'sql' parameter";
        }

        var reason = args.TryGetValue("reason", out var reasonObj) && reasonObj is string r ? r : "LLM investigation";

        try
        {
            var queryRequest = new QueryRequest
            {
                Sql = sql,
                RequestedBy = "Ollama"
            };

            var result = await _queryExecutor!.ExecuteReadOnlyQueryAsync(queryRequest);

            if (!result.Succeeded)
            {
                return $"Query failed: {result.ErrorMessage}";
            }

            // Format results for LLM analysis
            return FormatQueryResultsForLlm(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool call execute_sql_query failed");
            return $"Error executing query: {ex.Message}";
        }
    }

    private static string FormatQueryResultsForLlm(QueryResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Query executed successfully. Returned {result.RowCount} rows in {result.ExecutionMilliseconds}ms.");
        sb.AppendLine();
        sb.AppendLine("Columns: " + string.Join(", ", result.ColumnNames));
        sb.AppendLine();
        
        if (result.RowCount > 0)
        {
            sb.AppendLine("Sample rows (up to 10):");
            var sampleRows = result.Rows.Take(10);
            foreach (var row in sampleRows)
            {
                sb.Append("  ");
                foreach (var col in result.ColumnNames)
                {
                    var value = row.TryGetValue(col, out var v) ? v : null;
                    sb.Append($"{col}={value ?? "NULL"}, ");
                }
                sb.AppendLine();
            }
            
            if (result.RowCount > 10)
            {
                sb.AppendLine($"  ... and {result.RowCount - 10} more rows");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Strips <think>...</think> blocks from qwen3.5 responses (thinking mode content).
    /// qwen3.5 uses these tags to show reasoning, but we want clean output for users.
    /// </summary>
    private static string StripThinkingContent(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = ThinkingContentRegex.Replace(text, string.Empty);
        return result.Trim();
    }

    /// <summary>
    /// Stateful filter for removing <think>...</think> blocks from streaming responses.
    /// Handles cases where thinking tags span multiple chunks.
    /// </summary>
    private sealed class StreamingThinkingFilter
    {
        private readonly StringBuilder _buffer = new();
        private bool _insideThinking;

        public IEnumerable<string> ProcessChunk(string chunk)
        {
            _buffer.Append(chunk);

            while (_buffer.Length > 0)
            {
                if (!_insideThinking)
                {
                    var thinkIndex = _buffer.ToString().IndexOf("<think>", StringComparison.Ordinal);
                    
                    if (thinkIndex >= 0)
                    {
                        // Yield everything before <think>
                        if (thinkIndex > 0)
                        {
                            var output = _buffer.ToString(0, thinkIndex);
                            yield return output;
                        }
                        
                        // Remove everything up to and including <think>
                        _buffer.Remove(0, thinkIndex + 7); // 7 = "<think>".Length
                        _insideThinking = true;
                    }
                    else if (CouldStartThinkingTag(_buffer.ToString()))
                    {
                        // Buffer ends with partial "<think>" - wait for more chunks
                        yield break;
                    }
                    else
                    {
                        // No thinking tag found, yield everything
                        var output = _buffer.ToString();
                        _buffer.Clear();
                        yield return output;
                        yield break;
                    }
                }
                else // _insideThinking
                {
                    var endIndex = _buffer.ToString().IndexOf("</think>", StringComparison.Ordinal);
                    
                    if (endIndex >= 0)
                    {
                        // Remove thinking content and closing tag
                        _buffer.Remove(0, endIndex + 8); // 8 = "</think>".Length
                        
                        // Remove trailing whitespace after </think>
                        while (_buffer.Length > 0 && char.IsWhiteSpace(_buffer[0]))
                        {
                            _buffer.Remove(0, 1);
                        }
                        
                        _insideThinking = false;
                    }
                    else if (CouldStartEndThinkingTag(_buffer.ToString()))
                    {
                        // Buffer ends with partial "</think>" - wait for more
                        yield break;
                    }
                    else
                    {
                        // Inside thinking block, discard content and wait for closing tag
                        _buffer.Clear();
                        yield break;
                    }
                }
            }
        }

        private static bool CouldStartThinkingTag(string text)
        {
            // Check if buffer ends with a partial "<think>" tag
            return text.EndsWith("<") ||
                   text.EndsWith("<t") ||
                   text.EndsWith("<th") ||
                   text.EndsWith("<thi") ||
                   text.EndsWith("<thin") ||
                   text.EndsWith("<think");
        }

        private static bool CouldStartEndThinkingTag(string text)
        {
            // Check if buffer ends with a partial "</think>" tag
            return text.EndsWith("<") ||
                   text.EndsWith("</") ||
                   text.EndsWith("</t") ||
                   text.EndsWith("</th") ||
                   text.EndsWith("</thi") ||
                   text.EndsWith("</thin") ||
                   text.EndsWith("</think");
        }
    }

}
