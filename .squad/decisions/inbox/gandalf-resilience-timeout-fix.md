# Resilience Handler Timeout Configuration

**Date:** 2026-02-22  
**By:** Gandalf (Lead)  
**Status:** Resolved

## Problem
Chat requests were timing out at exactly 30 seconds despite multiple timeout configuration attempts:
1. Ollama HttpClient timeout set to 2 minutes
2. ASP.NET Core request timeout set to 5 minutes  
3. Frontend fetch timeout set to 180 seconds

Error showed .NET TimeSpan format "00:00:30", indicating backend timeout.

## Root Cause
Aspire's `AddStandardResilienceHandler()` in ServiceDefaults applies a Polly resilience pipeline with a **30-second total request timeout** to all HttpClients by default. This timeout takes precedence over `HttpClient.Timeout`.

## Solution
Configure `HttpStandardResilienceOptions` for the "ollamaModel" named HttpClient:

```csharp
builder.Services.Configure<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>("ollamaModel", options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});
```

## Key Learning
When using Aspire ServiceDefaults:
- Standard resilience handler timeout (30s default) overrides HttpClient.Timeout
- Must explicitly configure resilience options for long-running operations
- Named HttpClient configuration allows per-client timeout customization

## Layers of Timeout (in order)
1. **HttpClient.Timeout** (2 minutes) — transport layer
2. **Resilience handler total request timeout** (5 minutes) — **was the bottleneck**
3. **ASP.NET request timeout** (5 minutes) — server layer  
4. **Frontend fetch timeout** (180 seconds) — client layer

## Impact
Chat can now handle long-running LLM operations (multi-step tool calling, complex queries) without premature timeout.

## Files Modified
- `src/SqlAuditedQueryTool.App/Program.cs` (lines 45-49)
