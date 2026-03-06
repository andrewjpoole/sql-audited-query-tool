using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OllamaSharp;
using SqlAuditedQueryTool.Core.Interfaces;
using SqlAuditedQueryTool.Core.Interfaces.Llm;
using SqlAuditedQueryTool.Core.Models;
using SqlAuditedQueryTool.Core.Models.Llm;
using SqlAuditedQueryTool.Database;
using SqlAuditedQueryTool.Audit;
using SqlAuditedQueryTool.Llm;
using SqlAuditedQueryTool.Llm.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddOllamaApiClient("ollamaModel");

// Remove Polly resilience (retry, circuit breaker, timeout) from the Ollama chat client.
// The global resilience handler is added by ServiceDefaults for all HttpClients, but chat
// requests should not be retried — the user expects to cancel and retry manually.
// CommunityToolkit.Aspire.OllamaSharp registers the HttpClient as "{connectionName}_httpClient".
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
builder.Services.AddHttpClient("ollamaModel_httpClient").RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

// Configure timeout for Ollama HTTP client
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>>(sp =>
{
    var ollamaOptions = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return new ConfigureNamedOptions<HttpClientFactoryOptions>("ollamaModel_httpClient", options =>
    {
        options.HttpClientActions.Add(client =>
        {
            client.Timeout = ollamaOptions.ChatTimeout;
        });
    });
});

// Bridge OllamaSharp's IOllamaApiClient to Microsoft.Extensions.AI's IChatClient
builder.Services.AddScoped<IChatClient>(sp =>
{
    var chatClient = sp.GetRequiredService<IOllamaApiClient>();
    return (IChatClient)chatClient;
});

builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddAuditServices(builder.Configuration);
builder.Services.AddLlmServices(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configure request timeout for long-running LLM chat operations
// Default ASP.NET Core timeout is 30 seconds - extend to 5 minutes to support multi-step tool calling
builder.Services.AddRequestTimeouts(options =>
{
    // Set default policy to 5 minutes (300 seconds)
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddSpaStaticFiles(config =>
{
    config.RootPath = "ClientApp/dist";
});

var app = builder.Build();

// Log startup diagnostics
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
logger.LogInformation("=== SQL Audited Query Tool Starting ===");
logger.LogInformation("Connection string 'db' configured: {Configured}", app.Configuration.GetConnectionString("db") is not null);
logger.LogInformation("Environment: {Env}", app.Environment.EnvironmentName);

// Log timeout configuration for troubleshooting
var ollamaOptions = app.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;
logger.LogInformation("Ollama HttpClient Timeout: {Timeout} seconds", ollamaOptions.ChatTimeoutSeconds);
logger.LogInformation("Polly resilience handlers removed from Ollama chat client");
logger.LogInformation("ASP.NET Request Timeout: 5 minutes (300 seconds)");

app.UseCors();
app.UseRequestTimeouts();
app.MapDefaultEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Diagnostics — test DB and LLM connectivity
app.MapGet("/api/diagnostics", async (IServiceProvider sp, IConfiguration config, CancellationToken ct) =>
{
    var diag = new Dictionary<string, object?>();

    // Check connection string
    var connStr = config.GetConnectionString("db");
    diag["db_connection_string_configured"] = connStr is not null;
    diag["db_connection_string_preview"] = connStr is not null
        ? connStr[..Math.Min(connStr.Length, 50)] + "..."
        : null;

    // Test DB connection
    try
    {
        var connFactory = sp.GetRequiredService<IConnectionFactory>();
        await using var conn = await connFactory.CreateConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1";
        await cmd.ExecuteScalarAsync(ct);
        diag["db_status"] = "connected";
    }
    catch (Exception ex)
    {
        diag["db_status"] = "failed";
        diag["db_error"] = ex.Message;
    }

    // Test LLM / IChatClient
    try
    {
        var chatClient = sp.GetService<IChatClient>();
        diag["llm_chat_client_registered"] = chatClient is not null;
        diag["llm_chat_client_type"] = chatClient?.GetType().Name;
    }
    catch (Exception ex)
    {
        diag["llm_chat_client_registered"] = false;
        diag["llm_error"] = ex.Message;
    }

    // Test ILlmService
    try
    {
        var llmService = sp.GetService<ILlmService>();
        diag["llm_service_registered"] = llmService is not null;
    }
    catch (Exception ex)
    {
        diag["llm_service_registered"] = false;
        diag["llm_service_error"] = ex.Message;
    }

    // Test ISchemaProvider
    try
    {
        var schemaProvider = sp.GetService<ISchemaProvider>();
        diag["schema_provider_registered"] = schemaProvider is not null;
    }
    catch (Exception ex)
    {
        diag["schema_provider_registered"] = false;
        diag["schema_provider_error"] = ex.Message;
    }

    return Results.Ok(diag);
});

// LLM Chat — supports tool calling and chat history
app.MapPost("/api/chat", async (
    ChatRequest request, 
    ILlmService llmService, 
    ISchemaProvider schemaProvider,
    IQueryHistoryStore queryHistoryStore,
    IQueryExecutor executor,
    IAuditLogger auditLogger,
    IChatHistoryStore chatHistoryStore,
    HttpContext context, 
    CancellationToken ct) =>
{
    logger.LogInformation("POST /api/chat: SessionId={SessionId}, SystemPrompt={SystemPrompt}, MessageCount={Count}, Stream={Stream}, IncludeSchema={IncludeSchema}",
        request.SessionId, request.SystemPrompt != null, request.Messages?.Count ?? 0, request.Stream ?? false, request.IncludeSchema ?? false);
    try
    {
        // Get or create chat session
        ChatSession? session = null;
        if (request.SessionId.HasValue)
        {
            session = await chatHistoryStore.GetSessionAsync(request.SessionId.Value);
            if (session == null)
            {
                logger.LogWarning("Chat session {SessionId} not found, creating new session", request.SessionId.Value);
                session = await chatHistoryStore.CreateSessionAsync("Investigation Chat");
            }
        }
        else
        {
            session = await chatHistoryStore.CreateSessionAsync("Investigation Chat");
        }

        // Save user message to history
        if (request.Messages is { Count: > 0 })
        {
            var userMessage = request.Messages.Last();
            var userHistoryMsg = new ChatMessageHistory
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = userMessage.Role,
                Content = userMessage.Content,
                Timestamp = DateTimeOffset.UtcNow
            };
            session = await chatHistoryStore.AddMessageAsync(session.Id, userHistoryMsg);
        }

        var llmRequest = new LlmChatRequest
        {
            SystemPrompt = request.SystemPrompt,
            Messages = (request.Messages ?? []).Select(m => new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage { Role = m.Role, Content = m.Content }).ToList(),
            SchemaContext = request.IncludeSchema == true
                ? await schemaProvider.GetSchemaAsync(ct)
                : null
        };

        // Common variables used in both streaming and non-streaming paths
        LlmResponse response;
        var executedQueries = new List<object>();
        object? firstSuggestion;
        object? firstExecutedQuery;

        if (request.Stream == true)
        {
            context.Response.ContentType = "text/event-stream";
            
            // Phase 1: Handle tool calling with non-streaming ChatAsync
            // Tool calls require the full response to detect, so we use ChatAsync here
            response = await llmService.ChatAsync(llmRequest, ct);

            while (response.ToolCalls.Count > 0)
            {
                logger.LogInformation("LLM requested {ToolCallCount} tool calls", response.ToolCalls.Count);

                foreach (var toolCall in response.ToolCalls)
                {
                    logger.LogInformation("Executing tool: {ToolName}", toolCall.ToolName);
                    
                    // Send tool_start event
                    var toolStartEvent = JsonSerializer.Serialize(new
                    {
                        type = "tool_start",
                        tool = toolCall.ToolName,
                        args = toolCall.Arguments
                    });
                    await context.Response.WriteAsync($"data: {toolStartEvent}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                    
                    // Special handling for execute_sql_query to capture structured data
                    if (toolCall.ToolName == "execute_sql_query" && toolCall.Arguments.TryGetValue("sql", out var sqlObj) && sqlObj is string sql)
                    {
                        // Execute query through unified pipeline (executor -> audit -> history)
                        var queryRequest = new QueryRequest
                        {
                            Sql = sql,
                            RequestedBy = "Ollama"
                        };
                        
                        var structuredResult = await executor.ExecuteReadOnlyQueryAsync(queryRequest);
                        var audit = await auditLogger.LogQueryAsync(queryRequest, structuredResult, request.GitHubIssueNumber, request.AzDoWorkItemId);
                        
                        // Save to query history
                        var historyEntry = new QueryHistory
                        {
                            Id = Guid.NewGuid(),
                            Sql = sql,
                            RequestedBy = "Ollama",
                            Source = QuerySource.AI,
                            RequestTimestamp = queryRequest.Timestamp,
                            RowCount = structuredResult.RowCount,
                            ColumnCount = structuredResult.ColumnCount,
                            ColumnNames = structuredResult.ColumnNames,
                            ExecutionMilliseconds = structuredResult.ExecutionMilliseconds,
                            Succeeded = structuredResult.Succeeded,
                            ErrorMessage = structuredResult.ErrorMessage,
                            GitHubIssueUrl = audit.GitHubIssueUrl,
                            AzDoWorkItemUrl = audit.AzDoWorkItemUrl
                        };
                        await queryHistoryStore.AddAsync(historyEntry);

                        executedQueries.Add(new
                        {
                            historyId = historyEntry.Id,
                            sql,
                            rowCount = structuredResult.RowCount,
                            executionTimeMs = structuredResult.ExecutionMilliseconds,
                            auditUrl = audit.GitHubIssueUrl,
                            result = new
                            {
                                resultSets = structuredResult.ResultSets.Select(rs => new
                                {
                                    columns = rs.ColumnNames.Select(n => new { name = n, type = "unknown" }),
                                    rows = rs.Rows,
                                    rowCount = rs.RowCount
                                }).ToList(),
                                executionTimeMs = structuredResult.ExecutionMilliseconds
                            }
                        });
                    }
                    
                    // Execute tool through LLM service (handles all tools including code context)
                    var toolResult = await llmService.ExecuteToolCallAsync(toolCall, ct);

                    // Save tool result to chat history
                    var toolHistoryMsg = new ChatMessageHistory
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Role = "tool",
                        Content = toolResult,
                        Timestamp = DateTimeOffset.UtcNow,
                        ToolCallId = toolCall.ToolCallId,
                        ToolName = toolCall.ToolName
                    };
                    session = await chatHistoryStore.AddMessageAsync(session.Id, toolHistoryMsg);

                    // Send tool_result event
                    var toolResultEvent = JsonSerializer.Serialize(new
                    {
                        type = "tool_result",
                        tool = toolCall.ToolName,
                        success = true
                    });
                    await context.Response.WriteAsync($"data: {toolResultEvent}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);

                    // Add tool result to messages and continue conversation
                    llmRequest.Messages.Add(new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage
                    {
                        Role = "tool",
                        Content = toolResult
                    });
                }

                // Get next response from LLM
                response = await llmService.ChatAsync(llmRequest, ct);
            }

            // Phase 2: Stream the final text response with thinking support
            // Use StreamChatAsync to deliver text incrementally via SSE events.
            // After tool calls, this re-generates the response with full conversation context.
            // Without tool calls, this is the primary (and only) response generation.
            var fullTextBuilder = new StringBuilder();
            await foreach (var chunk in llmService.StreamChatAsync(llmRequest, ct))
            {
                var eventType = chunk.IsThinking ? "thinking" : "text";
                var sseEvent = JsonSerializer.Serialize(new
                {
                    type = eventType,
                    content = chunk.Content
                });
                await context.Response.WriteAsync($"data: {sseEvent}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);

                if (!chunk.IsThinking)
                    fullTextBuilder.Append(chunk.Content);
            }

            var streamedText = fullTextBuilder.ToString();

            // Build response from streamed text for history/validation
            response = new LlmResponse
            {
                Text = streamedText,
                SuggestedQueries = SqlAuditedQueryTool.Llm.Services.OllamaLlmService.ParseSuggestedQueries(streamedText),
                ToolCalls = []
            };

            // Save assistant response to history
            await chatHistoryStore.AddMessageAsync(session.Id, new ChatMessageHistory
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                Role = "assistant",
                Content = response.Text,
                Timestamp = DateTimeOffset.UtcNow
            });

            firstSuggestion = response.SuggestedQueries.FirstOrDefault();
            firstExecutedQuery = executedQueries.FirstOrDefault();

            // Validate suggested queries against schema if available, with retry logic
            if (llmRequest.SchemaContext is not null && response.SuggestedQueries.Count > 0)
            {
                const int maxRetries = 2;
                int retryCount = 0;

                while (retryCount < maxRetries)
                {
                    // Validate all suggested queries
                    var hasWarnings = false;
                    foreach (var sq in response.SuggestedQueries)
                    {
                        sq.SchemaWarnings = SqlAuditedQueryTool.Llm.Services.SqlSchemaValidator.Validate(sq.Sql, llmRequest.SchemaContext);
                        if (sq.SchemaWarnings.Count > 0)
                        {
                            hasWarnings = true;
                        }
                    }

                    // If no warnings, we're done
                    if (!hasWarnings) break;

                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        // Max retries reached, send warnings to frontend
                        break;
                    }

                    // Send schema_retry event to notify frontend
                    var retryEvent = JsonSerializer.Serialize(new
                    {
                        type = "schema_retry",
                        attempt = retryCount,
                        maxAttempts = maxRetries
                    });
                    await context.Response.WriteAsync($"data: {retryEvent}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);

                    // Build feedback message for LLM
                    var feedbackParts = new List<string>();
                    foreach (var sq in response.SuggestedQueries.Where(sq => sq.SchemaWarnings.Count > 0))
                    {
                        feedbackParts.Add($"Query:\n{sq.Sql}\n\nSchema issues:\n- {string.Join("\n- ", sq.SchemaWarnings)}");
                    }
                    var feedbackMessage = $"Your suggested query has schema validation issues:\n\n{string.Join("\n\n", feedbackParts)}\n\nPlease fix the query and suggest a corrected version.";

                    // Add feedback as assistant message (preserving tool call flow)
                    llmRequest.Messages.Add(new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage
                    {
                        Role = "user",
                        Content = feedbackMessage
                    });

                    // Request corrected query from LLM
                    response = await llmService.ChatAsync(llmRequest, ct);

                    // Save assistant response to history
                    await chatHistoryStore.AddMessageAsync(session.Id, new ChatMessageHistory
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Role = "assistant",
                        Content = response.Text,
                        Timestamp = DateTimeOffset.UtcNow
                    });

                    // Update firstSuggestion for the retry
                    firstSuggestion = response.SuggestedQueries.FirstOrDefault();
                }
            }

            // Send done event with full structured data
            var doneEvent = JsonSerializer.Serialize(new
            {
                type = "done",
                sessionId = session.Id,
                message = response.Text,
                executedQueries,
                executedQuery = firstExecutedQuery != null ? ((dynamic)firstExecutedQuery).sql : null,
                executedResult = firstExecutedQuery != null ? ((dynamic)firstExecutedQuery).result : null,
                suggestion = firstSuggestion is not null
                    ? new { sql = ((dynamic)firstSuggestion).Sql, explanation = "", isFixQuery = ((dynamic)firstSuggestion).IsFixQuery, schemaWarnings = ((dynamic)firstSuggestion).SchemaWarnings, isReadOnly = ((dynamic)firstSuggestion).IsReadOnly }
                    : (object?)null
            });
            await context.Response.WriteAsync($"data: {doneEvent}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            
            return Results.Empty;
        }

        // Non-streaming: Handle tool calling loop
        response = await llmService.ChatAsync(llmRequest, ct);

        // Tool calling loop
        while (response.ToolCalls.Count > 0)
        {
            logger.LogInformation("LLM requested {ToolCallCount} tool calls", response.ToolCalls.Count);

            foreach (var toolCall in response.ToolCalls)
            {
                logger.LogInformation("Executing tool: {ToolName}", toolCall.ToolName);
                
                // Special handling for execute_sql_query to capture structured data
                if (toolCall.ToolName == "execute_sql_query" && toolCall.Arguments.TryGetValue("sql", out var sqlObj) && sqlObj is string sql)
                {
                    // Execute query through unified pipeline (executor -> audit -> history)
                    var queryRequest = new QueryRequest
                    {
                        Sql = sql,
                        RequestedBy = "Ollama"
                    };
                    
                    var structuredResult = await executor.ExecuteReadOnlyQueryAsync(queryRequest);
                    var audit = await auditLogger.LogQueryAsync(queryRequest, structuredResult, request.GitHubIssueNumber, request.AzDoWorkItemId);
                    
                    // Save to query history
                    var historyEntry = new QueryHistory
                    {
                        Id = Guid.NewGuid(),
                        Sql = sql,
                        RequestedBy = "Ollama",
                        Source = QuerySource.AI,
                        RequestTimestamp = queryRequest.Timestamp,
                        RowCount = structuredResult.RowCount,
                        ColumnCount = structuredResult.ColumnCount,
                        ColumnNames = structuredResult.ColumnNames,
                        ExecutionMilliseconds = structuredResult.ExecutionMilliseconds,
                        Succeeded = structuredResult.Succeeded,
                        ErrorMessage = structuredResult.ErrorMessage,
                        GitHubIssueUrl = audit.GitHubIssueUrl,
                        AzDoWorkItemUrl = audit.AzDoWorkItemUrl
                    };
                    await queryHistoryStore.AddAsync(historyEntry);

                    executedQueries.Add(new
                    {
                        historyId = historyEntry.Id,
                        sql,
                        rowCount = structuredResult.RowCount,
                        executionTimeMs = structuredResult.ExecutionMilliseconds,
                        auditUrl = audit.GitHubIssueUrl,
                        result = new
                        {
                            resultSets = structuredResult.ResultSets.Select(rs => new
                            {
                                columns = rs.ColumnNames.Select(n => new { name = n, type = "unknown" }),
                                rows = rs.Rows,
                                rowCount = rs.RowCount
                            }).ToList(),
                            executionTimeMs = structuredResult.ExecutionMilliseconds
                        }
                    });
                }
                
                // Execute tool through LLM service (handles all tools including code context)
                var toolResult = await llmService.ExecuteToolCallAsync(toolCall, ct);

                // Save tool result to chat history
                var toolHistoryMsg = new ChatMessageHistory
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    Role = "tool",
                    Content = toolResult,
                    Timestamp = DateTimeOffset.UtcNow,
                    ToolCallId = toolCall.ToolCallId,
                    ToolName = toolCall.ToolName
                };
                session = await chatHistoryStore.AddMessageAsync(session.Id, toolHistoryMsg);

                // Add tool result to messages and continue conversation
                llmRequest.Messages.Add(new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage
                {
                    Role = "tool",
                    Content = toolResult
                });
            }

            // Get next response from LLM
            response = await llmService.ChatAsync(llmRequest, ct);
        }

        // Save assistant response to history
        await chatHistoryStore.AddMessageAsync(session.Id, new ChatMessageHistory
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "assistant",
            Content = response.Text,
            Timestamp = DateTimeOffset.UtcNow
        });

        firstSuggestion = response.SuggestedQueries.FirstOrDefault();
        firstExecutedQuery = executedQueries.FirstOrDefault();

        // Validate suggested queries against schema if available, with retry logic
        if (llmRequest.SchemaContext is not null && response.SuggestedQueries.Count > 0)
        {
            const int maxRetries = 2;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                // Validate all suggested queries
                var hasWarnings = false;
                foreach (var sq in response.SuggestedQueries)
                {
                    sq.SchemaWarnings = SqlAuditedQueryTool.Llm.Services.SqlSchemaValidator.Validate(sq.Sql, llmRequest.SchemaContext);
                    if (sq.SchemaWarnings.Count > 0)
                    {
                        hasWarnings = true;
                    }
                }

                // If no warnings, we're done
                if (!hasWarnings) break;

                retryCount++;
                if (retryCount >= maxRetries)
                {
                    // Max retries reached, send warnings in response
                    break;
                }

                // Build feedback message for LLM
                var feedbackParts = new List<string>();
                foreach (var sq in response.SuggestedQueries.Where(sq => sq.SchemaWarnings.Count > 0))
                {
                    feedbackParts.Add($"Query:\n{sq.Sql}\n\nSchema issues:\n- {string.Join("\n- ", sq.SchemaWarnings)}");
                }
                var feedbackMessage = $"Your suggested query has schema validation issues:\n\n{string.Join("\n\n", feedbackParts)}\n\nPlease fix the query and suggest a corrected version.";

                // Add feedback as user message
                llmRequest.Messages.Add(new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage
                {
                    Role = "user",
                    Content = feedbackMessage
                });

                // Request corrected query from LLM
                response = await llmService.ChatAsync(llmRequest, ct);

                // Save assistant response to history
                await chatHistoryStore.AddMessageAsync(session.Id, new ChatMessageHistory
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    Role = "assistant",
                    Content = response.Text,
                    Timestamp = DateTimeOffset.UtcNow
                });

                // Update firstSuggestion for the retry
                firstSuggestion = response.SuggestedQueries.FirstOrDefault();
            }
        }
        
        return Results.Ok(new
        {
            sessionId = session.Id,
            message = response.Text,
            executedQueries,
            // Frontend compatibility - single query/result
            executedQuery = firstExecutedQuery != null ? ((dynamic)firstExecutedQuery).sql : null,
            executedResult = firstExecutedQuery != null ? ((dynamic)firstExecutedQuery).result : null,
            suggestion = firstSuggestion is not null
                ? new { sql = ((dynamic)firstSuggestion).Sql, explanation = "", isFixQuery = ((dynamic)firstSuggestion).IsFixQuery, schemaWarnings = ((dynamic)firstSuggestion).SchemaWarnings, isReadOnly = ((dynamic)firstSuggestion).IsReadOnly }
                : (object?)null
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POST /api/chat failed");
        return Results.Json(new { message = ex.Message }, statusCode: 500);
    }
});

// Query suggestion — takes natural language, returns SQL suggestion
app.MapPost("/api/query/suggest", async (QuerySuggestRequest request, IQueryAssistant assistant, ISchemaProvider schemaProvider, CancellationToken ct) =>
{
    var schema = await schemaProvider.GetSchemaAsync(ct);
    var suggestion = await assistant.SuggestQueryAsync(request.NaturalLanguageRequest, schema, ct);
    return Results.Ok(suggestion);
});

// Schema metadata — returns table/column metadata (never row data)
app.MapGet("/api/schema", async (ISchemaProvider schemaProvider, CancellationToken ct) =>
{
    try
    {
        var schema = await schemaProvider.GetSchemaAsync(ct);
        return Results.Ok(schema);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "GET /api/schema failed");
        return Results.Json(new { message = ex.Message }, statusCode: 500);
    }
});

// Schema completions — Monaco autocomplete powered by embeddings (Phase 1)
app.MapPost("/api/completions/schema", async (
    CompletionContext context, 
    ICompletionService completionService,
    CancellationToken ct) =>
{
    try
    {
        logger.LogInformation("POST /api/completions/schema: Prefix={Prefix}, Context={Context}, Line={Line}", 
            context.Prefix, context.Context, context.CursorLine);
        
        var completions = await completionService.GetSchemaCompletionsAsync(context, ct);
        
        logger.LogInformation("POST /api/completions/schema: Returning {Count} completions", completions.Count);
        
        return Results.Ok(completions);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POST /api/completions/schema failed");
        return Results.Json(new { message = ex.Message }, statusCode: 500);
    }
});

// Query execution — runs readonly SQL and logs to audit trail
app.MapPost("/api/query/execute", async (
    ExecuteQueryRequest request,
    IQueryExecutor executor,
    IAuditLogger auditLogger,
    IQueryHistoryStore historyStore,
    ILogger<Program> logger) =>
{
    try
    {
        var queryRequest = new QueryRequest
        {
            Sql = request.Sql,
            RequestedBy = request.Source == "AI" ? "Ollama" : "anonymous", // TODO: replace with authenticated user
            ExecutionPlanMode = request.ExecutionPlanMode ?? ExecutionPlanMode.None
        };
        var result = await executor.ExecuteReadOnlyQueryAsync(queryRequest);
        var audit = await auditLogger.LogQueryAsync(queryRequest, result, request.GitHubIssueNumber, request.AzDoWorkItemId);
        
        logger.LogInformation("API: Query executed - {ResultSetCount} result set(s), {TotalRows} total rows, {ExecutionMs}ms, HasPlan={HasPlan}",
            result.ResultSets.Count, result.RowCount, result.ExecutionMilliseconds, result.HasExecutionPlan);
        
        // Save to query history
        var historyEntry = new QueryHistory
        {
            Id = Guid.NewGuid(),
            Sql = request.Sql,
            RequestedBy = queryRequest.RequestedBy,
            Source = request.Source == "AI" ? QuerySource.AI : QuerySource.User,
            RequestTimestamp = queryRequest.Timestamp,
            RowCount = result.RowCount,
            ColumnCount = result.ColumnCount,
            ColumnNames = result.ColumnNames,
            ExecutionMilliseconds = result.ExecutionMilliseconds,
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            GitHubIssueUrl = audit.GitHubIssueUrl,
            AzDoWorkItemUrl = audit.AzDoWorkItemUrl,
            IncludedExecutionPlan = request.ExecutionPlanMode != null && request.ExecutionPlanMode != ExecutionPlanMode.None
        };
        await historyStore.AddAsync(historyEntry);
        
        return Results.Ok(new
        {
            succeeded = result.Succeeded,
            errorMessage = result.ErrorMessage,
            historyId = historyEntry.Id,
            resultSets = result.ResultSets.Select(rs => new
            {
                columns = rs.ColumnNames.Select(n => new { name = n, type = "unknown" }),
                rows = rs.Rows,
                rowCount = rs.RowCount
            }).ToList(),
            executionTimeMs = result.ExecutionMilliseconds,
            auditUrl = audit.GitHubIssueUrl,
            executionPlanXml = result.ExecutionPlanXml,
            // Legacy compatibility
            columns = result.ColumnNames.Select(n => new { name = n, type = "unknown" }),
            rows = result.Rows,
            rowCount = result.RowCount
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POST /api/query/execute failed");
        return Results.Json(new { message = ex.Message }, statusCode: 500);
    }
});

// Query history — retrieves past query executions
app.MapGet("/api/query/history", async (IQueryHistoryStore historyStore, int? limit) =>
{
    var history = await historyStore.GetAllAsync(limit ?? 100);
    return Results.Ok(history.Select(h => new
    {
        id = h.Id,
        sql = h.Sql,
        requestedBy = h.RequestedBy,
        source = h.Source.ToString().ToLower(),
        timestamp = h.RequestTimestamp,
        rowCount = h.RowCount,
        columnCount = h.ColumnCount,
        executionTimeMs = h.ExecutionMilliseconds,
        succeeded = h.Succeeded,
        errorMessage = h.ErrorMessage,
        auditUrl = h.GitHubIssueUrl,
        azDoAuditUrl = h.AzDoWorkItemUrl,
        includedExecutionPlan = h.IncludedExecutionPlan
    }));
});

// === Write Script Simulator endpoints ===

// Simulate a write query — returns execution plan without executing
app.MapPost("/api/simulation/execute", async (
    SimulateRequest request,
    ISimulationService simulationService,
    CancellationToken ct) =>
{
    var simRequest = new SimulationRequest
    {
        Sql = request.Sql,
        RequestedBy = "anonymous" // TODO: replace with authenticated user
    };
    var result = await simulationService.SimulateAsync(simRequest, ct);
    return Results.Ok(new
    {
        isValid = result.IsValid,
        validationErrors = result.ValidationErrors,
        warnings = result.Warnings,
        estimatedAffectedRows = result.EstimatedAffectedRows,
        executionPlanXml = result.ExecutionPlanXml,
        executionMilliseconds = result.ExecutionMilliseconds,
        succeeded = result.Succeeded,
        errorMessage = result.ErrorMessage
    });
});

// Generate sql-script-runner scripts
app.MapPost("/api/simulation/generate-scripts", (
    GenerateScriptsRequest request,
    IScriptGeneratorService scriptGeneratorService) =>
{
    var genRequest = new ScriptGenerationRequest
    {
        Sql = request.Sql,
        RepositoryKey = request.RepositoryKey,
        WorkItemId = request.WorkItemId,
        Purpose = request.Purpose,
        ExpectedAffectedRows = request.ExpectedAffectedRows,
        RequestedBy = "anonymous" // TODO: replace with authenticated user
    };
    var result = scriptGeneratorService.GenerateScripts(genRequest);
    
    if (!result.Succeeded)
        return Results.BadRequest(new { message = result.ErrorMessage });
    
    return Results.Ok(new
    {
        succeeded = result.Succeeded,
        querySqlContent = result.QuerySqlContent,
        updateSqlContent = result.UpdateSqlContent,
        outputDirectory = result.OutputDirectory,
        errorMessage = result.ErrorMessage
    });
});

// List configured sql-script-runner repositories
app.MapGet("/api/simulation/repositories", (IOptions<SqlScriptRunnerOptions> options) =>
{
    return Results.Ok(options.Value.Repositories.Select(kvp => new
    {
        key = kvp.Key,
        name = kvp.Value,
        path = Path.Combine(options.Value.ReposBaseDirectory, kvp.Value)
    }));
});

// Chat history endpoints
app.MapGet("/api/chat/sessions", async (IChatHistoryStore chatHistoryStore, int? limit) =>
{
    var sessions = await chatHistoryStore.GetAllSessionsAsync(limit ?? 50);
    return Results.Ok(sessions.Select(s => new
    {
        id = s.Id,
        title = s.Title,
        createdAt = s.CreatedAt,
        lastMessageAt = s.LastMessageAt,
        messageCount = s.Messages.Count
    }));
});

app.MapGet("/api/chat/sessions/{sessionId:guid}", async (Guid sessionId, IChatHistoryStore chatHistoryStore) =>
{
    var session = await chatHistoryStore.GetSessionAsync(sessionId);
    if (session == null)
    {
        return Results.NotFound(new { message = "Chat session not found" });
    }

    return Results.Ok(new
    {
        id = session.Id,
        title = session.Title,
        createdAt = session.CreatedAt,
        lastMessageAt = session.LastMessageAt,
        messages = session.Messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            timestamp = m.Timestamp,
            toolCallId = m.ToolCallId,
            toolName = m.ToolName
        })
    });
});

app.MapDelete("/api/chat/sessions/{sessionId:guid}", async (Guid sessionId, IChatHistoryStore chatHistoryStore) =>
{
    await chatHistoryStore.DeleteSessionAsync(sessionId);
    return Results.NoContent();
});

app.UseStaticFiles();

// SPA middleware only for non-API paths — prevents HTML responses for /api/* errors
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    appBranch =>
    {
        appBranch.UseSpaStaticFiles();
        appBranch.UseSpa(spa =>
        {
            spa.Options.SourcePath = "ClientApp";

            if (app.Environment.IsDevelopment())
            {
                spa.UseProxyToSpaDevelopmentServer("http://localhost:5173");
            }
        });
    }
);

app.Run();

// Request DTOs for API endpoints
record ChatRequest(Guid? SessionId, string? SystemPrompt, List<ChatMessageDto> Messages, bool? Stream, bool? IncludeSchema, int? GitHubIssueNumber, int? AzDoWorkItemId);
record ChatMessageDto(string Role, string Content);
record QuerySuggestRequest(string NaturalLanguageRequest);
record ExecuteQueryRequest(string Sql, string? Source, ExecutionPlanMode? ExecutionPlanMode, int? GitHubIssueNumber, int? AzDoWorkItemId);
record SimulateRequest(string Sql);
record GenerateScriptsRequest(string Sql, string RepositoryKey, int WorkItemId, string Purpose, int ExpectedAffectedRows);
