# Decision: SSE Streaming Architecture with Thinking Support

**Author:** Radagast (LLM Engineer)  
**Date:** 2026-03-06  
**Status:** Implemented

## Context

The `/api/chat` streaming endpoint was using `ChatAsync` (non-streaming) even when `stream: true` was requested. The frontend received the complete response only in the `done` event. Additionally, thinking content from qwen3.5 was silently discarded.

## Decision

### Two-phase streaming architecture:
1. **Tool resolution phase**: Uses `ChatAsync` (non-streaming) for tool-calling rounds, since tool calls require the full response for detection. SSE `tool_start`/`tool_result` events keep the frontend informed.
2. **Text delivery phase**: Uses `StreamChatAsync` (streaming) for the final text response, emitting incremental SSE events with `type: "text"` or `type: "thinking"`.

### StreamChunk abstraction:
- New `StreamChunk(string Content, bool IsThinking)` record replaces raw `string` in the streaming interface.
- `StreamingThinkingFilter` now tags thinking content instead of discarding it.
- Frontend receives distinct `thinking` and `text` SSE event types.

## SSE Event Types (full list)
- `tool_start` — tool execution beginning
- `tool_result` — tool execution complete
- `text` — incremental response text chunk
- `thinking` — incremental thinking/reasoning chunk
- `schema_retry` — schema validation retry
- `done` — final structured response with session data

## Trade-offs
- Tool-calling rounds are not streamed (ChatAsync is used). This is acceptable since tool execution provides its own SSE feedback.
- After tool calls, the final response is re-generated via StreamChatAsync, which means one extra LLM call. This is the cost of real streaming after tool resolution.

## Impact
- **Frontend team**: Must handle `thinking` and incremental `text` SSE events.
- **Config**: `ThinkingEnabled` set to `true` for qwen3.5.
