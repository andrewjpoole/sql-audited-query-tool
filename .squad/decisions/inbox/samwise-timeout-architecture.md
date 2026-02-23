# Timeout Architecture for LLM Chat Operations

**Date:** 2026-02-22  
**By:** Samwise (Backend)  
**Status:** Implemented

## Decision

Established a three-layer timeout architecture for LLM chat operations with properly ordered timeouts to ensure graceful failure at the appropriate layer.

## Timeout Chain (Shortest to Longest)

1. **Ollama HttpClient: 120 seconds**
   - Controls timeout for individual HTTP calls to Ollama API
   - Configured via `OllamaOptions.ChatTimeoutSeconds` in appsettings.json
   - Location: `src/SqlAuditedQueryTool.App/Program.cs`
   - Will timeout first if a single LLM inference call takes too long

2. **Frontend fetch: 180 seconds**
   - Controls client-side wait time before aborting the request
   - Default parameter in `chat()` function
   - Location: `src/SqlAuditedQueryTool.App/ClientApp/src/api/queryApi.ts`
   - Provides buffer for multi-step tool calling loops

3. **ASP.NET Core request: 300 seconds**
   - Server-level timeout for the entire HTTP request lifecycle
   - Configured via `AddRequestTimeouts()` middleware
   - Location: `src/SqlAuditedQueryTool.App/Program.cs`
   - Safety net for exceptionally long operations

## Rationale

- **Ollama timeout (120s)** is the first to fire if the LLM model itself is slow or stuck
- **Frontend timeout (180s)** gives the tool-calling loop time to execute multiple queries (which may take several Ollama calls)
- **ASP.NET Core timeout (300s)** is the outermost safety net to prevent runaway requests

## Root Cause of Previous Failures

The first two timeout fixes only addressed backend layers (Ollama HttpClient and ASP.NET Core request timeout). The actual bottleneck was the frontend fetch timeout at 60 seconds, which was aborting requests before the backend could complete them.

## Key Lesson

**Always check the entire timeout chain from client to server, not just the backend.** The timeout that fires first is the one that matters.

## Files Modified

- `src/SqlAuditedQueryTool.App/ClientApp/src/api/queryApi.ts` - Frontend timeout: 60s → 180s
- `src/SqlAuditedQueryTool.App/Program.cs` - ASP.NET Core request timeout: 30s → 300s (previous fix)
- `src/SqlAuditedQueryTool.App/appsettings.json` - Ollama HttpClient: default 30s → 120s (previous fix)
