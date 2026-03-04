# Decision: Strip Thinking Mode Content from qwen3.5 Responses

**Date:** 2026-02-24  
**Author:** Radagast (LLM Engineer)  
**Status:** Implemented (Updated 2026-02-28 with streaming fix)

## Context

qwen3.5:27b (new Gated DeltaNet architecture model) has **thinking mode enabled by default**. This causes responses to include `<think>...\n</think>\n\n` blocks containing the model's internal reasoning process before the actual answer.

When this content was exposed to users, it:
- Caused 500 errors (depending on how frontend handled it)
- Cluttered the chat interface with verbose reasoning
- Confused users who just wanted the answer

## Decision

**Always strip `<think>...</think>` blocks from LLM responses** before returning them to the frontend/user.

Implementation (updated 2026-02-28):

**Non-streaming path (`ChatAsync`):**
1. Static compiled regex: `private static readonly Regex ThinkingContentRegex = new(@"<think>.*?</think>\s*", RegexOptions.Singleline | RegexOptions.Compiled)`
2. Applied once to complete response text
3. Fast and efficient (regex compiled once at class load)

**Streaming path (`StreamChatAsync`) — CRITICAL FIX:**
1. Created `StreamingThinkingFilter` class with stateful processing
2. Maintains buffer and `_insideThinking` boolean across chunks
3. Handles edge cases where tags split across chunks:
   - Detects partial `<think>` at chunk boundary (e.g., `<thi`)
   - Suppresses all content between `<think>` and `</think>`
   - Only yields clean content outside thinking blocks
4. **Why not regex per-chunk?** Thinking blocks span multiple chunks:
   ```
   Chunk 1: "<think>Let me analyze"
   Chunk 2: " this step by step"
   Chunk 3: "</think>\n\nThe answer"
   ```
   No single chunk contains both tags → regex never matches → content leaks

## Rationale

- **User-focused design**: Users care about answers, not the model's internal reasoning
- **Clean interface**: Chat UI should show concise, actionable responses
- **Model agnostic**: Works with any future model that uses similar thinking tags
- **Performance**: Static compiled regex for non-streaming, efficient stateful parser for streaming
- **Flexibility**: If we want to expose reasoning later, we can add a toggle/parameter

## Alternatives Considered

1. **Disable thinking mode via Ollama API** — Not all models support this, and thinking mode improves output quality
2. **Show thinking in collapsible section** — Adds UI complexity for a feature most users won't use
3. **Stream thinking separately** — Would require significant refactoring of streaming logic
4. **Parse thinking content for insights** — Out of scope; we just need clean output for now
5. **Per-chunk regex (REJECTED)** — Fails when tags span chunks (see streaming fix above)

## Implications

- **Future models**: If they use different tags (e.g., `<reasoning>`, `<internal>`), we'll need to update the pattern
- **Power users**: No way to see reasoning currently; could add opt-in later if requested
- **Tool calling**: Thinking content appears before tool calls, so our tool extraction logic still works
- **Streaming correctness**: Stateful filter ensures no thinking content leaks even when chunks split mid-tag

## Files Modified

- `src/SqlAuditedQueryTool.Llm/Services/OllamaLlmService.cs`
  - Added static `ThinkingContentRegex` field
  - Added nested `StreamingThinkingFilter` class
  - Updated `StreamChatAsync()` to use stateful filter
  - Updated `StripThinkingContent()` to use static regex

## Related

- Charter constraint: "LLM must NEVER be exposed to database row data" — thinking mode doesn't change this, but it reinforces the need for content filtering
- Could apply similar filtering for database result sets if models start echoing raw data
