# Chat API JSON Case Sensitivity Fix

**Date:** 2026-02-22  
**By:** Samwise (Backend Dev)  
**Status:** Implemented

## Decision
Configured ASP.NET Core's JSON serialization to be case-insensitive for all API endpoints.

## Context
The chat interface was getting "Error: Failed to fetch" when calling `/api/chat`. Investigation revealed:
- Frontend sends JSON with camelCase properties: `{ messages: [...], includeSchema: true }`
- Backend `ChatRequest` record expects PascalCase: `Messages`, `IncludeSchema`
- ASP.NET Core's default System.Text.Json deserialization is case-sensitive
- Silent deserialization failure → null values → downstream exceptions

## Implementation
Added to `Program.cs`:
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
```

Also added request logging to `/api/chat` endpoint for easier debugging.

## Impact
- ✅ Resolves "Failed to fetch" error in chat interface
- ✅ Makes all API endpoints resilient to casing differences
- ✅ Improves frontend/backend interoperability (JavaScript camelCase ↔ C# PascalCase)
- ⚠️ Applies globally to all endpoints (desired behavior for this app)

## Alternatives Considered
1. **Change frontend to use PascalCase** — rejected because camelCase is JavaScript/TypeScript convention
2. **Use `[JsonPropertyName]` attributes on DTOs** — rejected because it's verbose and error-prone at scale
3. **Custom model binder** — rejected as overkill for this scenario

## Recommendation for Team
This is the standard pattern for .NET backends serving JavaScript/TypeScript frontends. All future API endpoints benefit from this configuration automatically.
