using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models.Llm;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace SqlAuditedQueryTool.Llm.Services;

/// <summary>
/// LLM assistant that has access to code context tools for analyzing Entity Framework code.
/// </summary>
public sealed class CodeContextAssistant
{
    private readonly IChatClient _chatClient;
    private readonly ICodeContextService _codeContext;
    private readonly ILogger<CodeContextAssistant> _logger;

    public CodeContextAssistant(
        IChatClient chatClient,
        ICodeContextService codeContext,
        ILogger<CodeContextAssistant> logger)
    {
        _chatClient = chatClient;
        _codeContext = codeContext;
        _logger = logger;
    }

    /// <summary>
    /// Chat with the LLM with code context tools available.
    /// </summary>
    public async Task<string> ChatWithCodeContextAsync(
        string userMessage,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        systemPrompt ??= 
            "You are a helpful code analysis assistant. You have access to tools that let you " +
            "read files, list files, search code, and analyze Entity Framework DbContext classes. " +
            "Use these tools to answer questions about the codebase.";

        var messages = new List<AIChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        // Create a chat client with our code context tools
        var toolClient = _chatClient.AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var options = new ChatOptions
        {
            Tools = GetCodeContextTools()
        };

        _logger.LogInformation("Starting chat with code context tools. User message: {Message}", userMessage);

        var response = await toolClient.GetResponseAsync(messages, options, cancellationToken);

        return response.Text ?? "No response";
    }

    /// <summary>
    /// Stream chat with code context tools.
    /// </summary>
    public async IAsyncEnumerable<string> StreamChatWithCodeContextAsync(
        string userMessage,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        systemPrompt ??= 
            "You are a helpful code analysis assistant. You have access to tools that let you " +
            "read files, list files, search code, and analyze Entity Framework DbContext classes. " +
            "Use these tools to answer questions about the codebase.";

        var messages = new List<AIChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage)
        };

        var toolClient = _chatClient.AsBuilder()
            .UseFunctionInvocation()
            .Build();

        var options = new ChatOptions
        {
            Tools = GetCodeContextTools()
        };

        await foreach (var chunk in toolClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Text))
            {
                yield return chunk.Text;
            }
        }
    }

    private List<AITool> GetCodeContextTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(ReadFileAsync, "ReadFile"),
            AIFunctionFactory.Create(ListFilesAsync, "ListFiles"),
            AIFunctionFactory.Create(SearchCodeAsync, "SearchCode"),
            AIFunctionFactory.Create(AnalyzeEntityFrameworkContextAsync, "AnalyzeEntityFrameworkContext"),
            AIFunctionFactory.Create(AddContextDirectoryAsync, "AddContextDirectory"),
            AIFunctionFactory.Create(RemoveContextDirectoryAsync, "RemoveContextDirectory"),
            AIFunctionFactory.Create(ListContextDirectoriesAsync, "ListContextDirectories")
        };
    }

    #region Tool Implementations

    [Description("Read the content of a specific file")]
    private async Task<string> ReadFileAsync(
        [Description("The path to the file to read")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested to read file: {Path}", path);
            var result = await _codeContext.ReadFileAsync(path, cancellationToken);
            
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
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [Description("List files in a directory matching a pattern")]
    private async Task<string> ListFilesAsync(
        [Description("The directory to search in")] string directory,
        [Description("The file pattern to match (e.g., *.cs, *DbContext.cs)")] string pattern = "*.cs",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested to list files in {Directory} with pattern {Pattern}", directory, pattern);
            var result = await _codeContext.ListFilesAsync(directory, pattern, cancellationToken);
            
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
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [Description("Search for code patterns across files (like grep)")]
    private async Task<string> SearchCodeAsync(
        [Description("The regex pattern to search for")] string searchPattern,
        [Description("The directory to search in (default: current directory)")] string directory = ".",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested code search for pattern: {Pattern} in {Directory}", searchPattern, directory);
            var result = await _codeContext.SearchCodeAsync(searchPattern, directory, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                searchPattern = result.SearchPattern,
                totalCount = result.TotalCount,
                truncated = result.Truncated,
                matches = result.Matches.Select(m => new
                {
                    filePath = m.FilePath,
                    lineNumber = m.LineNumber,
                    lineContent = m.LineContent,
                    context = m.Context
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching code for pattern: {Pattern}", searchPattern);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [Description("Analyze Entity Framework DbContext classes and extract entity definitions, properties, and relationships")]
    private async Task<string> AnalyzeEntityFrameworkContextAsync(
        [Description("The directory to search for DbContext classes (default: current directory)")] string directory = ".",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested Entity Framework analysis in directory: {Directory}", directory);
            var contexts = await _codeContext.AnalyzeEntityFrameworkContextAsync(directory, cancellationToken);
            
            return JsonSerializer.Serialize(new
            {
                success = true,
                contextsFound = contexts.Count,
                contexts = contexts.Select(ctx => new
                {
                    contextName = ctx.ContextName,
                    filePath = ctx.FilePath,
                    entities = ctx.Entities.Select(e => new
                    {
                        name = e.Name,
                        tableName = e.TableName,
                        schemaName = e.SchemaName,
                        properties = e.Properties.Select(p => new
                        {
                            name = p.Name,
                            type = p.Type,
                            isNullable = p.IsNullable,
                            isKey = p.IsKey,
                            isRequired = p.IsRequired,
                            maxLength = p.MaxLength,
                            columnName = p.ColumnName,
                            dataAnnotations = p.DataAnnotations
                        }),
                        navigationProperties = e.NavigationProperties.Select(n => new
                        {
                            name = n.Name,
                            targetEntity = n.TargetEntity,
                            relationType = n.RelationType,
                            foreignKey = n.ForeignKey,
                            inverseProperty = n.InverseProperty
                        }),
                        indexes = e.Indexes,
                        configurations = e.Configurations
                    })
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing Entity Framework contexts in: {Directory}", directory);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [Description("Add a directory to the allowed list for this chat session, enabling file access")]
    private Task<string> AddContextDirectoryAsync(
        [Description("The full path to the directory to allow access to")] string directory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested to add context directory: {Directory}", directory);
            _codeContext.AddAllowedDirectory(directory);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Directory added to allowed list: {directory}",
                allowedDirectories = _codeContext.GetAllowedDirectories()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding context directory: {Directory}", directory);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    [Description("Remove a directory from the session allowed list")]
    private Task<string> RemoveContextDirectoryAsync(
        [Description("The directory path to remove from allowed list")] string directory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested to remove context directory: {Directory}", directory);
            var removed = _codeContext.RemoveAllowedDirectory(directory);
            
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = removed,
                message = removed ? $"Directory removed: {directory}" : $"Directory not found in session list: {directory}",
                allowedDirectories = _codeContext.GetAllowedDirectories()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing context directory: {Directory}", directory);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    [Description("List all directories currently allowed for code context access (both from config and session)")]
    private Task<string> ListContextDirectoriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("LLM requested list of context directories");
            var directories = _codeContext.GetAllowedDirectories();
            
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
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    #endregion
}
