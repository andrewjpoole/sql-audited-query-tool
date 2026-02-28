# Fix: Ollama Tool Calls Not Working

## Problem

The AI code context tools weren't working. The LLM (qwen2.5:7b) was making tool calls correctly, but the code couldn't extract them from the response.

**Root cause:** We were using Microsoft.Extensions.AI's `IChatClient` wrapper, which returns a `ChatResponse` object that doesn't expose the tool calls that Ollama is actually returning.

## Solution

Switched the non-streaming `ChatAsync()` method in `OllamaLlmService` to use `OllamaApiClient` directly (from OllamaSharp package) instead of going through Microsoft.Extensions.AI's `IChatClient`.

**Streaming remained unchanged** - it uses `IChatClient` for simplicity and doesn't need tool calls.

## Changes Made

### File: `src/SqlAuditedQueryTool.Llm/Services/OllamaLlmService.cs`

#### 1. Added Type Aliases for Clarity
```csharp
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AIChatRole = Microsoft.Extensions.AI.ChatRole;
using OllamaChatRole = OllamaSharp.Models.Chat.ChatRole;
```

This resolves the ambiguity between `Microsoft.Extensions.AI.ChatRole` and `OllamaSharp.Models.Chat.ChatRole`.

#### 2. Updated Constructor
Changed `OllamaApiClient` to `IOllamaApiClient` (the interface):
```csharp
private readonly IOllamaApiClient _ollamaClient;

public OllamaLlmService(
    IChatClient client,
    IOllamaApiClient ollamaClient,  // ← Changed from OllamaApiClient
    ...
```

#### 3. Rewrote ChatAsync() Method
**Before (broken):**
```csharp
var response = await _client.GetResponseAsync(messages, chatOptions, cancellationToken);
// response is ChatResponse - no access to tool_calls
```

**After (working):**
```csharp
var chatRequest = new ChatRequest
{
    Model = _options.Model,
    Messages = BuildOllamaMessages(request),
    Tools = BuildOllamaTools(),
    Stream = false
};

ChatResponseStream? finalResponse = null;
await foreach (var chunk in _ollamaClient.ChatAsync(chatRequest, cancellationToken))
{
    if (chunk != null) finalResponse = chunk;
}

var toolCalls = finalResponse?.Message != null 
    ? ExtractToolCallsFromOllama(finalResponse.Message) 
    : new List<ToolCallRequest>();
```

#### 4. Added BuildOllamaMessages() Method
Converts from our `LlmChatRequest` to OllamaSharp's `Message[]` format:
```csharp
private static Message[] BuildOllamaMessages(LlmChatRequest request)
{
    var messages = new List<Message>();
    // ... builds system message and user messages with OllamaChatRole
    return messages.ToArray();
}
```

#### 5. Added BuildOllamaTools() Method
Converts tool definitions to OllamaSharp's tool format using anonymous objects:
```csharp
private IEnumerable<object>? BuildOllamaTools()
{
    if (_codeContextService == null) return null;
    
    return new[]
    {
        new
        {
            type = "function",
            function = new
            {
                name = "ReadFile",
                description = "Read the content of a specific file",
                parameters = new { ... }
            }
        },
        // ... 6 more tools
    };
}
```

#### 6. Rewrote ExtractToolCallsFromOllama() Method
**Before:** Tried to extract from `ChatResponse` (which doesn't have tool_calls)
**After:** Extracts from OllamaSharp's `Message` object:
```csharp
private List<ToolCallRequest> ExtractToolCallsFromOllama(Message message)
{
    var toolCalls = new List<ToolCallRequest>();
    
    if (message?.ToolCalls != null)
    {
        foreach (var toolCall in message.ToolCalls)
        {
            // Parse arguments from toolCall.Function.Arguments
            // Add to toolCalls list
        }
    }
    
    return toolCalls;
}
```

#### 7. Removed Debug Logging
Removed all the reflection-based debug code that was trying to inspect `ChatResponse` properties.

## Technical Details

### OllamaSharp API Structure

- **Interface:** `IOllamaApiClient` (injected by Aspire's `AddOllamaApiClient()`)
- **Method:** `ChatAsync(ChatRequest, CancellationToken)` returns `IAsyncEnumerable<ChatResponseStream?>`
- **Response:** `ChatResponseStream` has a `Message` property
- **Tool Calls:** `Message.ToolCalls` contains `IEnumerable<Message.ToolCall>`
- **Tool Call Structure:** Each `ToolCall` has a `Function` with `Name` and `Arguments`

### Why This Works

1. **Direct API Access:** OllamaSharp's `IOllamaApiClient` directly maps to Ollama's `/api/chat` endpoint
2. **Full Response Structure:** OllamaSharp preserves the complete response including `tool_calls`
3. **Type Safety:** Using OllamaSharp's native types ensures we get the full response structure
4. **Backward Compatibility:** Streaming still uses `IChatClient` (which works fine for streaming text)

## Testing

### Unit Tests
All 96 tests pass ✅

### Manual Testing
Use the test guide in `test-tool-calls.md`:
1. Start the application
2. Ask: "Show me what directories I can access"
3. Expected: LLM calls `ListContextDirectories` tool and returns the list

## Files Changed

- `src/SqlAuditedQueryTool.Llm/Services/OllamaLlmService.cs` - Complete rewrite of non-streaming chat logic

## Verification

- ✅ Build succeeds with no errors (1 warning about nullable - fixed)
- ✅ All 96 tests pass
- ✅ Code compiles cleanly
- ✅ Tool definitions match OllamaSharp's expected format
- ✅ Tool call extraction uses correct response structure
