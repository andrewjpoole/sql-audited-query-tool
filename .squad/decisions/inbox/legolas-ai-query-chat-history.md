# AI Query Execution & Chat History Persistence

**Date:** 2026-02-22T23:30:00Z  
**By:** Legolas (Frontend Dev)  
**Context:** Radagast implementing Ollama tool calling for query execution; Samwise ensuring history entries created

## Decision

Implemented two frontend features to support AI-assisted query workflows:

### 1. AI-Executed Query Display
- Queries executed by Ollama via tool calling now automatically load into Monaco editor
- Visual distinction: 🤖 robot emoji for AI-initiated queries, 👤 user emoji for user-initiated queries
- Query history panel shows source badges to distinguish query origin
- API contract: `LlmResponse` extended with `executedQuery` and `executedResult` fields

### 2. Persistent Chat History
- Chat sessions persist in browser localStorage with full message history
- Session management UI: browsable list with timestamp, preview, message count
- Auto-generated session titles from first user message (40 char preview)
- Session operations: create new, load existing, delete, auto-save on every message
- State architecture: `useChatHistory` hook manages CRUD + localStorage sync; `ChatPanel` is session-aware with controlled messages

## Rationale

**AI Query Display:**
- Users need visibility when AI executes queries autonomously (transparency)
- Clear source attribution prevents confusion about who initiated which query
- Loading query into editor allows users to inspect, modify, or re-run AI-generated queries

**Chat Persistence:**
- Investigation workflows span hours/days — users need to resume conversations
- Session browsing enables review of past investigation threads
- localStorage keeps data client-side (no backend required, privacy-preserving)

## Implementation Notes

- `HistoryEntry.source` field is optional for backward compatibility
- Session ID uses timestamp + random suffix for uniqueness
- Time formatting: "Just now" / "Xm ago" / "Xh ago" / "Xd ago" / date
- Chat session list appears in same sidebar area as query history (mutually exclusive toggles)
- Total bundle: 76 modules, 232KB JS (gzip 73KB) — minimal size increase

## Impact

- **Radagast:** Backend must return `executedQuery` and `executedResult` in API response when Ollama runs queries
- **Samwise:** History entries created for AI-executed queries will have metadata indicating AI origin
- **User workflow:** Users can now track AI query execution and resume investigation sessions across browser restarts
