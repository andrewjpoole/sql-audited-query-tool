# Chat Response Timeout Configuration

**Date:** 2026-02-22  
**By:** Samwise (Backend Dev)  
**Status:** Implemented

## Decision

Increased LLM chat response timeout from 30 seconds to 120 seconds (2 minutes) and made it configurable via appsettings.json.

## Context

- Users were experiencing timeout errors during chat sessions with the LLM
- Complex queries requiring multiple tool calls or long-running analysis exceeded the 30-second default timeout
- Andrew requested a longer timeout (2 minutes) that can be configured without code changes

## Implementation

1. **Configuration class:** Added `ChatTimeoutSeconds` property to `OllamaOptions` with default value of 120
2. **HttpClient setup:** Modified `Program.cs` to configure the Ollama HttpClient timeout using `IConfigureOptions<HttpClientFactoryOptions>`
3. **Settings file:** Added `"ChatTimeoutSeconds": 120` to `appsettings.json` under the `Llm` section

## Key Files Modified

- `src\SqlAuditedQueryTool.Llm\Configuration\OllamaOptions.cs` — added timeout property
- `src\SqlAuditedQueryTool.App\Program.cs` — configured HttpClient timeout for "ollamaModel" named client
- `src\SqlAuditedQueryTool.App\appsettings.json` — added timeout setting

## Configuration

Users can now adjust the timeout in their appsettings:

```json
{
  "Llm": {
    "ChatTimeoutSeconds": 120
  }
}
```

## Technical Notes

- The timeout applies per HTTP request to Ollama
- For non-streaming chat with tool calling, each LLM call in the loop has this timeout
- For streaming chat, this is the timeout for establishing the stream (not for receiving chunks)
- Named HttpClient configuration must match the Aspire registration name ("ollamaModel")

## Why This Matters

- Prevents false timeouts during legitimate long-running LLM operations
- Allows operators to tune timeout based on their specific LLM model and workload
- Maintains flexibility without requiring code changes or recompilation
