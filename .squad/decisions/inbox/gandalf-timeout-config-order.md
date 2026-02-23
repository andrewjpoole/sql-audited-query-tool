# Aspire Resilience Configuration Order Fix

**By:** Gandalf (Lead)
**Date:** 2026-02-23
**Status:** Implemented

## Problem

After four attempts to fix the 30-second timeout on POST /api/chat, the error persisted:
```
Polly.Timeout.TimeoutRejectedException: The operation didn't complete within the allowed timeout of '00:00:30'
```

Previous attempts configured the resilience handler timeout but failed because of incorrect configuration order and scope.

## Root Cause

1. **ServiceDefaults applies global defaults:** `AddServiceDefaults()` calls `ConfigureHttpClientDefaults` which adds `AddStandardResilienceHandler()` with 30-second timeout to ALL HttpClients
2. **Configuration order matters:** Options must be configured BEFORE `AddServiceDefaults()` is called, not after
3. **Named client configuration doesn't work:** Trying to configure `HttpStandardResilienceOptions` with a named client scope ("ollamaModel") doesn't work when the resilience handler is applied via `ConfigureHttpClientDefaults`
4. **Duplicate ConfigureHttpClientDefaults:** Program.cs was calling `ConfigureHttpClientDefaults` a second time (lines 37-43), which was causing configuration conflicts

## Solution

Configure `HttpStandardResilienceOptions` globally BEFORE calling `AddServiceDefaults()`:

```csharp
// BEFORE AddServiceDefaults - configure options
builder.Services.Configure<Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions>(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
});

// NOW call AddServiceDefaults - it picks up the configured options
builder.AddServiceDefaults();
builder.AddOllamaApiClient("ollamaModel");
```

## Key Learnings

1. **Order of operations matters:** When using Aspire ServiceDefaults, configure options BEFORE calling `AddServiceDefaults()`
2. **Global vs Named configuration:** 
   - Options applied via `ConfigureHttpClientDefaults` need global configuration
   - Named client configuration only works for client-specific settings
3. **Don't call ConfigureHttpClientDefaults twice:** ServiceDefaults already calls it - calling it again causes conflicts
4. **Configuration scope:**
   - `Configure<HttpStandardResilienceOptions>(options => ...)` — affects ALL HttpClients
   - `Configure<HttpStandardResilienceOptions>("clientName", options => ...)` — only works if that specific client has its own resilience handler added

## Files Modified

- `src/SqlAuditedQueryTool.App/Program.cs` — Moved resilience configuration before AddServiceDefaults, removed duplicate ConfigureHttpClientDefaults

## Impact

All HttpClients now have 5-minute timeout instead of 30 seconds. This is acceptable for our application since:
- Chat operations can take several minutes with tool calling
- Query execution is fast but LLM processing is slow
- We already have frontend timeout at 180 seconds as the user-facing limit
