# Decision: Chat Timeout & SQL Block Detection

**By:** Legolas (Frontend Dev)  
**Date:** 2026-02-22T22:00:00Z  
**Status:** Implemented

## Context
Users reported two issues with chat functionality:
1. Chat API calls sometimes timeout without clear feedback
2. Chat responses containing SQL code aren't actionable

## Decision
Implemented timeout handling and SQL block detection in chat interface.

### Timeout Handling
- Added configurable timeout (60 seconds default) to `queryApi.chat()` using `AbortController`
- Timeout errors display user-friendly message: "Request timed out. The LLM is taking longer than expected. Please try again or simplify your question."
- Timeout errors are visually distinguished with ⏱️ emoji in chat bubble

### SQL Block Detection
- Chat messages now parse markdown SQL code blocks (```sql...```)
- Each detected SQL block renders as actionable card with "📝 Insert into Editor" button
- Uses same `onInsertSql` callback as existing suggestion cards for consistency
- Supports multiple SQL blocks per message

## Technical Details
- **API layer:** `queryApi.chat()` signature extended with optional `timeoutMs` parameter
- **Component layer:** `ChatPanel.tsx` extracts SQL blocks via regex, renders inline action buttons
- **CSS:** New styles for `.chat-sql-block`, `.chat-sql-code`, `.chat-sql-insert` with dark theme consistency

## Impact
- **User experience:** Clear feedback on timeouts, actionable SQL suggestions from LLM
- **Backend integration:** No backend changes required, timeout handled client-side
- **Future extensibility:** SQL block detection pattern can extend to other code types if needed
