# DI Scoping and Graceful Degradation

**By:** Samwise (Backend)  
**Date:** 2026-02-22  

## What
Fixed two critical issues that were blocking local development:

1. **Chat endpoint DI scope violation:** Changed `/api/chat` to inject `ISchemaProvider` as a method parameter instead of resolving from `app.Services` (root provider). Scoped services must be resolved from a scoped provider.

2. **Audit logger startup failure:** Made `GitHubAuditLogger` resilient to missing configuration. Instead of throwing exceptions when env vars aren't configured, it now logs a warning and operates in local-only mode (no GitHub posting, `GitHubIssueUrl = null`).

## Why
- **DI scoping:** Violating ASP.NET Core's service lifetime rules causes "Cannot resolve scoped service from root provider" exceptions. The framework's design is explicit: scoped services have per-request lifetime and can only be resolved within a request scope.

- **Graceful degradation:** External integrations (GitHub, email, monitoring, etc.) shouldn't block the app from starting. Local dev environments often don't have production credentials configured. Apps should degrade gracefully and remain functional with reduced features rather than crashing.

## Pattern Established
- **DI in minimal APIs:** Use method parameter injection for scoped/transient services. The framework handles scope correctly.
- **Optional external services:** Check config availability in constructor, set a flag (`_isConfigured`), log warnings, and provide fallback behavior (local logging, no-op, etc.) instead of throwing.

## Impact
App now starts and runs successfully in local dev environments without GitHub configuration. Chat endpoint no longer crashes when schema context is requested.
