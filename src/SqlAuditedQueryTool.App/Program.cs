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

// Configure resilience handler timeout AFTER AddServiceDefaults
// The ServiceDefaults ConfigureHttpClientDefaults already added the resilience handler
// Now we need to configure its options for ALL HttpClients (including Ollama)
// This MUST come after AddServiceDefaults but BEFORE AddOllamaApiClient
builder.Services.ConfigureAll<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
{
    // Extend total request timeout to 5 minutes for ALL HttpClients (including Ollama)
    // This overrides the default 30-second timeout from Aspire's standard resilience handler
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});

builder.AddOllamaApiClient("ollamaModel");

// Configure timeout for Ollama HTTP client
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
builder.Services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>>(sp =>
{
    var ollamaOptions = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return new ConfigureNamedOptions<HttpClientFactoryOptions>("ollamaModel", options =>
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
logger.LogInformation("Resilience Handler Total Request Timeout: 5 minutes (300 seconds)");
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

        if (request.Stream == true)
        {
            context.Response.ContentType = "text/event-stream";
            await foreach (var token in llmService.StreamChatAsync(llmRequest, ct))
            {
                await context.Response.WriteAsync($"data: {token}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
            await context.Response.WriteAsync("data: [DONE]\n\n", ct);
            return Results.Empty;
        }

        // Non-streaming: Handle tool calling loop
        var response = await llmService.ChatAsync(llmRequest, ct);
        var executedQueries = new List<object>();

        // Tool calling loop
        while (response.ToolCalls.Count > 0)
        {
            logger.LogInformation("LLM requested {ToolCallCount} tool calls", response.ToolCalls.Count);

            foreach (var toolCall in response.ToolCalls)
            {
                logger.LogInformation("Executing tool: {ToolName}", toolCall.ToolName);
                
                if (toolCall.ToolName == "execute_sql_query" && toolCall.Arguments.TryGetValue("sql", out var sqlObj) && sqlObj is string sql)
                {
                    // Execute query through unified pipeline (executor -> audit -> history)
                    var queryRequest = new QueryRequest
                    {
                        Sql = sql,
                        RequestedBy = "Ollama"
                    };
                    
                    var structuredResult = await executor.ExecuteReadOnlyQueryAsync(queryRequest);
                    var audit = await auditLogger.LogQueryAsync(queryRequest, structuredResult);
                    
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
                        GitHubIssueUrl = audit.GitHubIssueUrl
                    };
                    await queryHistoryStore.AddAsync(historyEntry);

                    executedQueries.Add(new
                    {
                        historyId = historyEntry.Id,
                        sql,
                        rowCount = structuredResult.RowCount,
                        executionTimeMs = structuredResult.ExecutionMilliseconds,
                        auditUrl = audit.GitHubIssueUrl
                    });
                    
                    // Format result for LLM (use the LLM service's tool call handler for consistent formatting)
                    var queryResult = await llmService.ExecuteToolCallAsync(toolCall, ct);

                    // Save tool result to chat history
                    var toolHistoryMsg = new ChatMessageHistory
                    {
                        Id = Guid.NewGuid(),
                        SessionId = session.Id,
                        Role = "tool",
                        Content = queryResult,
                        Timestamp = DateTimeOffset.UtcNow,
                        ToolCallId = toolCall.ToolCallId,
                        ToolName = toolCall.ToolName
                    };
                    session = await chatHistoryStore.AddMessageAsync(session.Id, toolHistoryMsg);

                    // Add tool result to messages and continue conversation
                    llmRequest.Messages.Add(new SqlAuditedQueryTool.Core.Models.Llm.ChatMessage
                    {
                        Role = "tool",
                        Content = queryResult
                    });
                }
            }

            // Get next response from LLM
            response = await llmService.ChatAsync(llmRequest, ct);
        }

        // Save assistant response to history
        var assistantHistoryMsg = new ChatMessageHistory
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = "assistant",
            Content = response.Text,
            Timestamp = DateTimeOffset.UtcNow
        };
        await chatHistoryStore.AddMessageAsync(session.Id, assistantHistoryMsg);

        var firstSuggestion = response.SuggestedQueries.FirstOrDefault();
        return Results.Ok(new
        {
            sessionId = session.Id,
            message = response.Text,
            executedQueries,
            suggestion = firstSuggestion is not null
                ? new { sql = firstSuggestion.Sql, explanation = "", isFixQuery = firstSuggestion.IsFixQuery }
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
            RequestedBy = request.Source == "AI" ? "Ollama" : "anonymous" // TODO: replace with authenticated user
        };
        var result = await executor.ExecuteReadOnlyQueryAsync(queryRequest);
        var audit = await auditLogger.LogQueryAsync(queryRequest, result);
        
        logger.LogInformation("API: Query executed - {ResultSetCount} result set(s), {TotalRows} total rows, {ExecutionMs}ms",
            result.ResultSets.Count, result.RowCount, result.ExecutionMilliseconds);
        
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
            GitHubIssueUrl = audit.GitHubIssueUrl
        };
        await historyStore.AddAsync(historyEntry);
        
        return Results.Ok(new
        {
            historyId = historyEntry.Id,
            resultSets = result.ResultSets.Select(rs => new
            {
                columns = rs.ColumnNames.Select(n => new { name = n, type = "unknown" }),
                rows = rs.Rows,
                rowCount = rs.RowCount
            }).ToList(),
            executionTimeMs = result.ExecutionMilliseconds,
            auditUrl = audit.GitHubIssueUrl,
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
        auditUrl = h.GitHubIssueUrl
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
record ChatRequest(Guid? SessionId, string? SystemPrompt, List<ChatMessageDto> Messages, bool? Stream, bool? IncludeSchema);
record ChatMessageDto(string Role, string Content);
record QuerySuggestRequest(string NaturalLanguageRequest);
record ExecuteQueryRequest(string Sql, string? Source);
